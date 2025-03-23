﻿using System.Linq;
using Content.Shared.Backmen.Surgery.Pain;
using Content.Shared.Backmen.Surgery.Wounds.Components;
using Content.Shared.Body.Organ;
using Content.Shared.FixedPoint;

namespace Content.Shared.Backmen.Surgery.Traumas.Systems;

public partial class TraumaSystem
{
    private void InitOrgans()
    {
        SubscribeLocalEvent<WoundableComponent, OrganIntegrityChangedEventOnWoundable>(OnOrganIntegrityChanged);
        SubscribeLocalEvent<WoundableComponent, OrganDamageSeverityChangedOnWoundable>(OnOrganSeverityChanged);
    }

    #region Event handling

    private void OnOrganIntegrityChanged(Entity<WoundableComponent> bodyPart, ref OrganIntegrityChangedEventOnWoundable args)
    {
        if (args.Organ.Comp.Body == null)
            return;

        if (!_consciousness.TryGetNerveSystem(args.Organ.Comp.Body.Value, out var nerveSys))
            return;

        var organs = _body.GetPartOrgans(args.Organ.Comp.Body.Value).ToList();

        var totalIntegrity = organs.Aggregate((FixedPoint2) 0, (current, organ) => current + organ.Component.OrganIntegrity);
        var totalIntegrityCap = organs.Aggregate((FixedPoint2) 0, (current, organ) => current + organ.Component.IntegrityCap);

        // Getting your organ turned into a blood mush inside you applies a LOT of internal pain, that can get you dead.
        if (!_pain.TryChangePainModifier(
                nerveSys.Value,
                bodyPart.Owner,
                "OrganDamage",
                totalIntegrityCap - totalIntegrity,
                nerveSys.Value.Comp))
        {
            _pain.TryAddPainModifier(
                nerveSys.Value,
                bodyPart.Owner,
                "OrganDamage",
                totalIntegrityCap - totalIntegrity,
                PainDamageTypes.TraumaticPain,
                nerveSys.Value.Comp);
        }
    }

    private void OnOrganSeverityChanged(Entity<WoundableComponent> bodyPart, ref OrganDamageSeverityChangedOnWoundable args)
    {
        if (args.Organ.Comp.Body == null)
            return;

        if (args.NewSeverity != OrganSeverity.Destroyed)
            return;

        _audio.PlayPvs(args.Organ.Comp.OrganDestroyedSound, args.Organ.Comp.Body.Value);
        _body.RemoveOrgan(args.Organ, args.Organ.Comp);

        if (_net.IsServer)
            QueueDel(args.Organ);
    }

    #endregion

    #region Public API

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

    #endregion

    #region Private API

    private void UpdateOrganIntegrity(EntityUid uid, OrganComponent organ)
    {
        var oldIntegrity = organ.OrganIntegrity;
        organ.OrganIntegrity = FixedPoint2.Clamp(organ.IntegrityModifiers
                .Aggregate((FixedPoint2) 0, (current, modifier) => current + modifier.Value),
                0,
                organ.IntegrityCap);

        if (oldIntegrity != organ.OrganIntegrity)
        {
            var ev = new OrganIntegrityChangedEvent(oldIntegrity, organ.OrganIntegrity);
            RaiseLocalEvent(uid, ref ev, true);

            if (_container.TryGetContainingContainer((uid, Transform(uid), MetaData(uid)), out var container))
            {
                var ev1 = new OrganIntegrityChangedEventOnWoundable((uid, organ), oldIntegrity, organ.OrganIntegrity);
                RaiseLocalEvent(container.Owner, ref ev1, true);
            }
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
            var ev = new OrganDamageSeverityChanged(organ.OrganSeverity, nearestSeverity);
            RaiseLocalEvent(uid, ref ev, true);

            if (_container.TryGetContainingContainer((uid, Transform(uid), MetaData(uid)), out var container))
            {
                var ev1 = new OrganDamageSeverityChangedOnWoundable((uid, organ), organ.OrganSeverity, nearestSeverity);
                RaiseLocalEvent(container.Owner, ref ev1, true);
            }
        }

        organ.OrganSeverity = nearestSeverity;
        Dirty(uid, organ);
    }

    #endregion
}
