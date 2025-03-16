﻿using System.Linq;
using Content.Shared.Backmen.Surgery.Wounds.Components;
using Content.Shared.Body.Organ;
using Content.Shared.Body.Part;
using Content.Shared.FixedPoint;
using Robust.Shared.Random;

namespace Content.Shared.Backmen.Surgery.Traumas.Systems;

public partial class TraumaSystem
{
    private const float OrganDamageSeverityThreshold = 12f;

    private void InitOrgans()
    {
        SubscribeLocalEvent<OrganComponent, OrganDamageSeverityChanged>(OnOrganSeverityChanged);
        SubscribeLocalEvent<OrganComponent, OrganDamagePointChangedEvent>(SomeOtherShitHappensHere);
    }

    #region Event handling

    private void OnOrganSeverityChanged(EntityUid organ, OrganComponent comp, OrganDamageSeverityChanged args)
    {
        if (args.NewSeverity != OrganSeverity.Destroyed)
            return;

        if (comp.Body != null && _consciousness.TryGetNerveSystem(comp.Body.Value, out var nerveSys))
        {
            // Getting your organ turned into a blood mush inside you applies a LOT of internal pain, that can get you dead.
            _pain.TryAddPainModifier(nerveSys.Value, nerveSys.Value, "OrganDestroyed", 20f, time: TimeSpan.FromSeconds(12f));

            _audio.PlayPvs(comp.OrganDestroyedSound, comp.Body.Value);
        }

        _body.RemoveOrgan(organ, comp);
        QueueDel(organ);
    }

    private void SomeOtherShitHappensHere(EntityUid organ, OrganComponent comp, OrganDamagePointChangedEvent args)
    {
        // When we get to this, make some organs work worse based on their state (of course you won't be able to breathe THAT good when your lung is pierced by a .50cal)
    }

    #endregion

    #region Public API

    public bool ApplyDamageToOrgan(EntityUid organ, FixedPoint2 severity, OrganComponent? organComp = null)
    {
        if (!Resolve(organ, ref organComp))
            return false;

        var newIntegrity = FixedPoint2.Clamp(organComp.OrganIntegrity - severity, 0, organComp.IntegrityCap);
        if (organComp.OrganIntegrity == newIntegrity)
            return false;

        organComp.OrganIntegrity = newIntegrity;
        var ev = new OrganDamagePointChangedEvent(organ, organComp.OrganIntegrity, severity);
        RaiseLocalEvent(organ, ref ev, true);

        UpdateOrganIntegrity(organ, organComp);
        Dirty(organ, organComp);
        return true;
    }

    public bool TryCreateOrganDamageModifier(EntityUid uid,
        FixedPoint2 severity,
        EntityUid effectOwner,
        string identifier,
        OrganComponent? organ = null)
    {
        if (!Resolve(uid, ref organ))
            return false;

        organ.IntegrityModifiers.Add((identifier, effectOwner), severity);
        UpdateOrganIntegrity(uid, organ);

        return true;
    }

    public bool TrySetOrganDamageModifier(EntityUid uid,
        FixedPoint2 severity,
        EntityUid effectOwner,
        string identifier,
        OrganComponent? organ = null)
    {
        if (!Resolve(uid, ref organ))
            return false;

        organ.IntegrityModifiers[(identifier, effectOwner)] = severity;
        UpdateOrganIntegrity(uid, organ);

        return true;
    }

    public bool TryChangeOrganDamageModifier(EntityUid uid,
        FixedPoint2 change,
        EntityUid effectOwner,
        string identifier,
        OrganComponent? organ = null)
    {
        if (!Resolve(uid, ref organ))
            return false;

        if (!organ.IntegrityModifiers.TryGetValue((identifier, effectOwner), out var value))
            return false;

        organ.IntegrityModifiers[(identifier, effectOwner)] = value + change;
        UpdateOrganIntegrity(uid, organ);

        return true;
    }

    public bool TryRemoveOrganDamageModifier(EntityUid uid,
        EntityUid effectOwner,
        string identifier,
        OrganComponent? organ = null)
    {
        if (!Resolve(uid, ref organ))
            return false;

        if (!organ.IntegrityModifiers.Remove((identifier, effectOwner)))
            return false;

        UpdateOrganIntegrity(uid, organ);
        return true;
    }

    public bool RandomOrganTraumaChance(EntityUid target, EntityUid woundInflicter, WoundableComponent woundable, FixedPoint2 severity)
    {
        var bodyPart = Comp<BodyPartComponent>(target);
        if (!bodyPart.Body.HasValue)
            return false; // No entity to apply pain to

        if (severity < OrganDamageSeverityThreshold)
            return false;

        var totalIntegrity =
            _body.GetPartOrgans(target, bodyPart)
                .Aggregate((FixedPoint2)0, (current, organ) => current + organ.Component.OrganIntegrity);

        if (totalIntegrity <= 0) // No surviving organs
            return false;

        // organ damage is like, very deadly, but not yet
        // so like, like, yeah, we don't want a disabler to induce some EVIL ASS organ damage with a 0,000001% chance and ruin your round
        // Very unlikely to happen if your woundables are in a good condition
        var chance =
            FixedPoint2.Clamp(
                woundable.WoundableIntegrity / woundable.IntegrityCap / totalIntegrity
                + Comp<WoundComponent>(woundInflicter).TraumasChances[TraumaType.OrganDamage],
                0,
                1);

        return _random.Prob((float) chance);
    }

    #endregion

    #region Private API

    private void UpdateOrganIntegrity(EntityUid uid, OrganComponent organ)
    {
        var oldIntegrity = organ.OrganIntegrity;

        // Makes more sense for this to just multiply the value no?
        organ.OrganIntegrity = FixedPoint2.Clamp(organ.IntegrityModifiers
                .Aggregate((FixedPoint2) 0, (current, modifier) => current + modifier.Value),
                0,
                organ.IntegrityCap);

        if (oldIntegrity != organ.OrganIntegrity)
        {
            var ev = new OrganDamagePointChangedEvent(uid, organ.OrganIntegrity, organ.OrganIntegrity - oldIntegrity);
            RaiseLocalEvent(uid, ref ev, true);

            // TODO: might want also to raise the event on the woundable.
        }

        var nearestSeverity = organ.OrganSeverity;
        foreach (var (severity, value) in organ.IntegrityThresholds.OrderByDescending(kv => kv.Value))
        {
            if (organ.OrganIntegrity < value)
                continue;

            nearestSeverity = severity;
            break;
        }

        if (nearestSeverity != organ.OrganSeverity)
        {
            var ev = new OrganDamageSeverityChanged(uid, organ.OrganSeverity, nearestSeverity);
            RaiseLocalEvent(uid, ref ev, true);
            // TODO: might want also to raise the event on the woundable.
        }
        organ.OrganSeverity = nearestSeverity;
        Dirty(uid, organ);
    }

    #endregion
}
