using System.Linq;
using Content.Shared.Backmen.Surgery.Pain;
using Content.Shared.Backmen.Surgery.Traumas.Components;
using Content.Shared.Backmen.Surgery.Wounds.Components;
using Content.Shared.Body;
using Content.Shared.Body.Part;
using Content.Shared.FixedPoint;
using Content.Shared.Movement.Components;
using JetBrains.Annotations;
using Robust.Shared.Audio;
using Robust.Shared.Utility;

namespace Content.Shared.Backmen.Surgery.Traumas.Systems;

public partial class TraumaSystem
{
    private const string BoneDamagePainId = "BoneDamage";

    /// <summary>
    /// Keeps traumatic bone pain in sync with current bone integrity.
    /// Clears pain when the bone is healthy; otherwise sets pain from remaining integrity loss.
    /// </summary>
    protected void SyncBoneDamagePain(
        EntityUid woundableUid,
        BoneComponent bone,
        FixedPoint2? integrityOverride = null)
    {
        if (!Body.TryGetWoundableBodyPartInfo(woundableUid, out var bodyUid, out _, out _))
            return;

        if (!Consciousness.TryGetNerveSystem(bodyUid, out var nerveSys))
            return;

        // BoneIntegrityChangedEvent fires before BoneIntegrity is written — allow callers to pass NewIntegrity.
        var integrity = integrityOverride ?? bone.BoneIntegrity;

        if (integrity >= bone.IntegrityCap)
        {
            Pain.TryRemovePainModifier(
                nerveSys.Value.Owner,
                woundableUid,
                BoneDamagePainId,
                nerveSys.Value.Comp);
            return;
        }

        var pain = (bone.IntegrityCap - integrity) * 2;
        if (pain <= FixedPoint2.Zero)
        {
            Pain.TryRemovePainModifier(
                nerveSys.Value.Owner,
                woundableUid,
                BoneDamagePainId,
                nerveSys.Value.Comp);
            return;
        }

        if (!Pain.TryChangePainModifier(
                nerveSys.Value.Owner,
                woundableUid,
                BoneDamagePainId,
                pain,
                nerveSys.Value.Comp,
                painType: PainType.TraumaticPain))
        {
            Pain.TryAddPainModifier(
                nerveSys.Value.Owner,
                woundableUid,
                BoneDamagePainId,
                pain,
                PainType.TraumaticPain,
                nerveSys.Value.Comp);
        }
    }

    /// <summary>
    /// Removes traumatic bone pain tied to a woundable once the bone is healthy again.
    /// </summary>
    protected void TryClearBoneDamagePain(EntityUid woundableUid)
    {
        if (!WoundableQuery.TryComp(woundableUid, out var woundable))
        {
            // Fall back to a blind remove if the woundable is gone.
            if (!Body.TryGetWoundableBodyPartInfo(woundableUid, out var bodyUid, out _, out _))
                return;

            if (!Consciousness.TryGetNerveSystem(bodyUid, out var nerveSys))
                return;

            Pain.TryRemovePainModifier(
                nerveSys.Value.Owner,
                woundableUid,
                BoneDamagePainId,
                nerveSys.Value.Comp);
            return;
        }

        var boneEnt = woundable.Bone.ContainedEntities.FirstOrNull();
        if (boneEnt == null || !BoneQuery.TryComp(boneEnt, out var boneComp))
        {
            if (!Body.TryGetWoundableBodyPartInfo(woundableUid, out var bodyUid, out _, out _))
                return;

            if (!Consciousness.TryGetNerveSystem(bodyUid, out var nerveSys))
                return;

            Pain.TryRemovePainModifier(
                nerveSys.Value.Owner,
                woundableUid,
                BoneDamagePainId,
                nerveSys.Value.Comp);
            return;
        }

        SyncBoneDamagePain(woundableUid, boneComp);
    }

    private void InitBones()
    {
        SubscribeLocalEvent<BoneComponent, BoneSeverityChangedEvent>(OnBoneSeverityChanged);
        SubscribeLocalEvent<BoneComponent, BoneIntegrityChangedEvent>(OnBoneIntegrityChanged);
    }

    #region Event Handling

    private void OnBoneSeverityChanged(Entity<BoneComponent> bone, ref BoneSeverityChangedEvent args)
    {
        if (bone.Comp.BoneWoundable == null)
            return;

        if (!Body.TryGetWoundableBodyPartInfo(bone.Comp.BoneWoundable.Value, out var bodyUid, out var partType, out _))
            return;

        SyncBoneDamagePain(bone.Comp.BoneWoundable.Value, bone.Comp);

        switch (args.NewSeverity)
        {
            case BoneSeverity.Damaged:
                _audio.PlayPvs(bone.Comp.BoneBreakSound, bodyUid, AudioParams.Default.WithVolume(-8f));
                break;

            case BoneSeverity.Broken:
                _audio.PlayPvs(bone.Comp.BoneBreakSound, bodyUid, AudioParams.Default.WithVolume(6f));

                if (partType == BodyPartType.Hand)
                {
                    _virtual.TrySpawnVirtualItemInHand(bone, bodyUid);
                }
                break;
        }
    }

    private void OnBoneIntegrityChanged(Entity<BoneComponent> bone, ref BoneIntegrityChangedEvent args)
    {
        if (bone.Comp.BoneWoundable == null)
            return;

        if (!Body.TryGetWoundableBodyPartInfo(bone.Comp.BoneWoundable.Value, out var bodyUid, out var partType, out _))
            return;

        if (args.NewIntegrity == bone.Comp.IntegrityCap)
        {
            if (partType == BodyPartType.Hand)
            {
                _virtual.DeleteInHandsMatching(bodyUid, bone);
            }

            if (TryGetWoundableTrauma(bone.Comp.BoneWoundable.Value, out var traumas, TraumaType.BoneDamage))
            {
                foreach (var trauma in traumas.Where(trauma => trauma.Comp.TraumaTarget == bone))
                {
                    RemoveTrauma(trauma);
                }
            }
        }

        // Keep analyzer pain in sync even when wounds (and trauma entities) are already gone.
        SyncBoneDamagePain(bone.Comp.BoneWoundable.Value, bone.Comp, args.NewIntegrity);

        switch (partType)
        {
            case BodyPartType.Leg:
            case BodyPartType.Foot:
                ProcessLegsState(bodyUid);

                break;
        }
    }

    #endregion

    #region Public API

    /// <summary>
    /// True if any body part has a bone that is not fully healthy (independent of wound trauma entities).
    /// </summary>
    [PublicAPI]
    public bool HasBodyBoneDamage(EntityUid body, BodyComponent? bodyComp = null)
    {
        if (!Resolve(body, ref bodyComp, false))
            return false;

        foreach (var woundable in Body.GetWoundableTargets(body, bodyComp))
        {
            if (HasWoundableBoneDamage(woundable))
                return true;
        }

        return false;
    }

    /// <summary>
    /// True if this woundable's bone is damaged/broken, even when the wound trauma entities are gone.
    /// </summary>
    [PublicAPI]
    public bool HasWoundableBoneDamage(EntityUid woundable, WoundableComponent? woundableComp = null)
    {
        if (!WoundableQuery.Resolve(woundable, ref woundableComp, false))
            return false;

        var boneEnt = woundableComp.Bone.ContainedEntities.FirstOrNull();
        if (boneEnt == null || !BoneQuery.TryComp(boneEnt, out var bone))
            return false;

        return bone.BoneSeverity != BoneSeverity.Normal || bone.BoneIntegrity < bone.IntegrityCap;
    }

    [PublicAPI]
    public virtual bool ApplyBoneTrauma(
        EntityUid boneEnt,
        Entity<WoundableComponent> woundable,
        Entity<TraumaInflicterComponent> inflicter,
        FixedPoint2 inflicterSeverity,
        BoneComponent? boneComp = null)
    {
        // Server-only execution
        return false;
    }

    [PublicAPI]
    public virtual bool SetBoneIntegrity(EntityUid bone, FixedPoint2 integrity, BoneComponent? boneComp = null)
    {
        // Server-only execution
        return false;
    }

    [PublicAPI]
    public virtual bool ApplyDamageToBone(EntityUid bone, FixedPoint2 severity, BoneComponent? boneComp = null)
    {
        // Server-only execution
        return false;
    }

    #endregion

    #region Private API

    protected void ProcessLegsState(EntityUid body)
    {
        if (!TryComp<BodyComponent>(body, out var bodyComp))
            return;

        var rawWalkSpeed = 0f; // just used to compare to actual speed values

        var walkSpeed = 0f;
        var sprintSpeed = 0f;
        var acceleration = 0f;

        foreach (var legEntity in bodyComp.LegEntities)
        {
            float partWalkSpeed;
            float partSprintSpeed;
            float partAcceleration;

            if (TryComp<MovementBodyPartComponent>(legEntity, out var movement))
            {
                partWalkSpeed = movement.WalkSpeed;
                partSprintSpeed = movement.SprintSpeed;
                partAcceleration = movement.Acceleration;
            }
            else
            {
                partWalkSpeed = MovementSpeedModifierComponent.DefaultBaseWalkSpeed;
                partSprintSpeed = MovementSpeedModifierComponent.DefaultBaseSprintSpeed;
                partAcceleration = MovementSpeedModifierComponent.DefaultAcceleration;
            }

            if (!TryComp<WoundableComponent>(legEntity, out var legWoundable))
                continue;

            var ent = legWoundable.Bone.ContainedEntities.FirstOrNull();
            if (!TryComp<BoneComponent>(ent, out var boneComp))
                continue;

            BodyPartSymmetry? legSymmetry = null;
            if (TryComp<BodyPartComponent>(legEntity, out var legPart))
                legSymmetry = legPart.Symmetry;
            else if (TryComp<OrganComponent>(legEntity, out var legOrgan) && legOrgan.Category is { } legCategory)
            {
                if (legCategory == "LegLeft")
                    legSymmetry = BodyPartSymmetry.Left;
                else if (legCategory == "LegRight")
                    legSymmetry = BodyPartSymmetry.Right;
            }

            var penalty = 1f;
            if (Body.TryGetWoundableTargetByType(body, BodyPartType.Foot, legSymmetry, out var footUid)
                && TryComp<WoundableComponent>(footUid, out var footWoundable))
            {
                var footBoneEnt = footWoundable.Bone.ContainedEntities.FirstOrNull();
                if (TryComp<BoneComponent>(footBoneEnt, out var footBone))
                {
                    penalty = footBone.BoneSeverity switch
                    {
                        BoneSeverity.Damaged => 0.77f,
                        BoneSeverity.Broken => 0.55f,
                        _ => penalty,
                    };
                }
            }
            else
            {
                penalty = 0.22f;
            }

            rawWalkSpeed += partWalkSpeed;

            partWalkSpeed *= penalty;
            partSprintSpeed *= penalty;
            partAcceleration *= penalty;

            switch (boneComp.BoneSeverity)
            {
                case BoneSeverity.Damaged:
                    walkSpeed += partWalkSpeed / 1.6f;
                    sprintSpeed += partSprintSpeed / 1.6f;
                    acceleration += partAcceleration / 1.6f;

                    break;
                case BoneSeverity.Normal:
                    walkSpeed += partWalkSpeed;
                    sprintSpeed += partSprintSpeed;
                    acceleration += partAcceleration;

                    break;
            }
        }

        var requiredLegs = Body.GetEffectiveRequiredLegs(body);
        rawWalkSpeed /= requiredLegs;
        walkSpeed /= requiredLegs;
        sprintSpeed /= requiredLegs;
        acceleration /= requiredLegs;

        _movementSpeed.ChangeBaseSpeed(body, walkSpeed, sprintSpeed, acceleration);
        if (walkSpeed < rawWalkSpeed / 2.7f)
        {
            _standing.Down(body);
        }
        else
        {
            _standing.Stand(body);
        }
    }

    #endregion
}
