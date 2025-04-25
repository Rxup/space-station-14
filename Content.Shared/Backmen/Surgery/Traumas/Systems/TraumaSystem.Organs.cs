using System.Linq;
using Content.Shared.Backmen.CCVar;
using Content.Shared.Backmen.Surgery.Pain;
using Content.Shared.Backmen.Surgery.Traumas.Components;
using Content.Shared.Backmen.Surgery.Wounds.Components;
using Content.Shared.Body.Organ;
using Content.Shared.FixedPoint;
using Content.Shared.Humanoid;
using JetBrains.Annotations;
using Robust.Shared.Audio;

namespace Content.Shared.Backmen.Surgery.Traumas.Systems;

public partial class TraumaSystem
{
    private const string OrganDamagePainIdentifier = "OrganDamage";

    private void InitOrgans()
    {
        SubscribeLocalEvent<OrganComponent, OrganIntegrityChangedEvent>(OnOrganIntegrityChanged);

        SubscribeLocalEvent<WoundableComponent, OrganIntegrityChangedEventOnWoundable>(OnOrganIntegrityOnWoundableChanged);
        SubscribeLocalEvent<WoundableComponent, OrganDamageSeverityChangedOnWoundable>(OnOrganSeverityChanged);
    }

    #region Event Handling

    private void OnOrganIntegrityChanged(Entity<OrganComponent> organ, ref OrganIntegrityChangedEvent args)
    {
        if (organ.Comp.Body == null)
            return;

        if (args.NewIntegrity < organ.Comp.IntegrityCap || !TryGetBodyTraumas(organ.Comp.Body.Value, out var traumas, TraumaType.OrganDamage))
            return;

        foreach (var trauma in traumas.Where(trauma => trauma.Comp.TraumaTarget == organ))
        {
            RemoveTrauma(trauma);
        }
    }

    private void OnOrganIntegrityOnWoundableChanged(Entity<WoundableComponent> bodyPart, ref OrganIntegrityChangedEventOnWoundable args)
    {
        if (args.Organ.Comp.Body == null)
            return;

        if (!Consciousness.TryGetNerveSystem(args.Organ.Comp.Body.Value, out var nerveSys))
            return;

        var organs = Body.GetPartOrgans(args.Organ.Comp.Body.Value).ToList();

        var totalIntegrity = organs.Aggregate(FixedPoint2.Zero, (current, organ) => current + organ.Component.OrganIntegrity);
        var totalIntegrityCap = organs.Aggregate(FixedPoint2.Zero, (current, organ) => current + organ.Component.IntegrityCap);

        // Getting your organ turned into a blood mush inside you applies a LOT of internal pain, that can get you dead.
        if (!Pain.TryChangePainModifier(
                nerveSys.Value,
                bodyPart.Owner,
                OrganDamagePainIdentifier,
                (totalIntegrityCap - totalIntegrity) / 2,
                nerveSys.Value.Comp))
        {
            Pain.TryAddPainModifier(
                nerveSys.Value,
                bodyPart.Owner,
                OrganDamagePainIdentifier,
                (totalIntegrityCap - totalIntegrity) / 2,
                PainDamageTypes.TraumaticPain,
                nerveSys.Value.Comp);
        }
    }

    private void OnOrganSeverityChanged(Entity<WoundableComponent> bodyPart, ref OrganDamageSeverityChangedOnWoundable args)
    {
        var body = args.Organ.Comp.Body;
        if (body == null)
            return;

        if (args.NewSeverity != OrganSeverity.Destroyed)
            return;

        if (Consciousness.TryGetNerveSystem(body.Value, out var nerveSys) && !_mobState.IsDead(body.Value))
        {
            var sex = Sex.Unsexed;
            if (TryComp<HumanoidAppearanceComponent>(body, out var humanoid))
                sex = humanoid.Sex;

            Pain.PlayPainSoundWithCleanup(
                body.Value,
                nerveSys.Value.Comp,
                nerveSys.Value.Comp.OrganDestructionReflexSounds[sex],
                AudioParams.Default.WithVolume(6f));

            _stun.TryParalyze(body.Value, nerveSys.Value.Comp.OrganDamageStunTime, true);
            _stun.TrySlowdown(
                body.Value,
                nerveSys.Value.Comp.OrganDamageStunTime * _organTraumaSlowdownTimeMultiplier,
                true,
                _organTraumaWalkSpeedSlowdown,
                _organTraumaRunSpeedSlowdown);
        }

        if (TryGetWoundableTrauma(bodyPart, out var traumas, TraumaType.OrganDamage, bodyPart))
        {
            foreach (var trauma in traumas)
            {
                if (trauma.Comp.TraumaTarget != args.Organ)
                    continue;

                RemoveTrauma(trauma);
            }
        }

        _audio.PlayPvs(args.Organ.Comp.OrganDestroyedSound, body.Value);
        Body.RemoveOrgan(args.Organ, args.Organ.Comp);

        if (_net.IsServer)
            QueueDel(args.Organ);
    }

    #endregion

    #region Public API

    [PublicAPI]
    public virtual bool TryCreateOrganDamageModifier(EntityUid uid,
        FixedPoint2 severity,
        EntityUid effectOwner,
        string identifier,
        OrganComponent? organ = null)
    {
        // Server-only execution
        return false;
    }

    [PublicAPI]
    public virtual bool TrySetOrganDamageModifier(EntityUid uid,
        FixedPoint2 severity,
        EntityUid effectOwner,
        string identifier,
        OrganComponent? organ = null)
    {
        // Server-only execution
        return false;
    }

    [PublicAPI]
    public virtual bool TryChangeOrganDamageModifier(
        EntityUid uid,
        FixedPoint2 change,
        EntityUid effectOwner,
        string identifier,
        OrganComponent? organ = null)
    {
        // Server-only execution
        return false;
    }

    [PublicAPI]
    public virtual bool TryRemoveOrganDamageModifier(
        EntityUid uid,
        EntityUid effectOwner,
        string identifier,
        OrganComponent? organ = null)
    {
        // Server-only execution
        return false;
    }

    #endregion
}
