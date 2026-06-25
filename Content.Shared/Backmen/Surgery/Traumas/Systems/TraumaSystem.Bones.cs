using System.Linq;
using Content.Shared.Backmen.Surgery.Traumas.Components;
using Content.Shared.Backmen.Surgery.Wounds.Components;
using Content.Shared.Body;
using Content.Shared.Body.Part;
using Content.Shared.FixedPoint;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using JetBrains.Annotations;
using Robust.Shared.Audio;
using Robust.Shared.Utility;

namespace Content.Shared.Backmen.Surgery.Traumas.Systems;

public partial class TraumaSystem
{
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
