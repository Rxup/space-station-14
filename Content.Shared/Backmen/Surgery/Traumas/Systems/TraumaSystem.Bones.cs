using System.Linq;
using Content.Shared.Backmen.Surgery.Traumas.Components;
using Content.Shared.Backmen.Surgery.Wounds.Components;
using Content.Shared.Body.Components;
using Content.Shared.Body.Part;
using Content.Shared.FixedPoint;
using Content.Shared.Movement.Components;
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

        var bodyComp = Comp<BodyPartComponent>(bone.Comp.BoneWoundable.Value);
        if (!bodyComp.Body.HasValue)
            return;

        switch (args.NewSeverity)
        {
            case BoneSeverity.Damaged:
                _audio.PlayPvs(bone.Comp.BoneBreakSound, bodyComp.Body.Value, AudioParams.Default.WithVolume(-8f));
                break;

            case BoneSeverity.Broken:
                _audio.PlayPvs(bone.Comp.BoneBreakSound, bodyComp.Body.Value, AudioParams.Default.WithVolume(6f));

                if (bodyComp.PartType == BodyPartType.Hand)
                {
                    _virtual.TrySpawnVirtualItemInHand(bone, bodyComp.Body.Value);
                }
                break;
        }
    }

    private void OnBoneIntegrityChanged(Entity<BoneComponent> bone, ref BoneIntegrityChangedEvent args)
    {
        if (bone.Comp.BoneWoundable == null)
            return;

        var bodyComp = Comp<BodyPartComponent>(bone.Comp.BoneWoundable.Value);
        if (!bodyComp.Body.HasValue)
            return;

        if (args.NewIntegrity == bone.Comp.IntegrityCap)
        {
            if (bodyComp.PartType == BodyPartType.Hand)
            {
                _virtual.DeleteInHandsMatching(bodyComp.Body.Value, bone);
            }

            if (TryGetWoundableTrauma(bone.Comp.BoneWoundable.Value, out var traumas, TraumaType.BoneDamage))
            {
                foreach (var trauma in traumas.Where(trauma => trauma.Comp.TraumaTarget == bone))
                {
                    RemoveTrauma(trauma);
                }
            }
        }

        switch (bodyComp.PartType)
        {
            case BodyPartType.Leg:
            case BodyPartType.Foot:
                ProcessLegsState(bodyComp.Body.Value);

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
            if (!TryComp<MovementBodyPartComponent>(legEntity, out var movement))
                continue;

            var partWalkSpeed = movement.WalkSpeed;
            var partSprintSpeed = movement.SprintSpeed;
            var partAcceleration = movement.Acceleration;

            if (!TryComp<WoundableComponent>(legEntity, out var legWoundable))
                continue;

            var ent = legWoundable.Bone.ContainedEntities.FirstOrNull();
            if (!TryComp<BoneComponent>(ent, out var boneComp))
                continue;

            // get the foot penalty
            var penalty = 1f;
            var footEnt =
                Body.GetBodyChildrenOfType(body,
                        BodyPartType.Foot,
                        symmetry: Comp<BodyPartComponent>(legEntity).Symmetry)
                    .FirstOrNull();

            if (footEnt != null)
            {
                if (TryComp<BoneComponent>(legWoundable.Bone.ContainedEntities.FirstOrNull(), out var footBone))
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
                // You are supposed to have one
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

        rawWalkSpeed /= bodyComp.RequiredLegs;
        walkSpeed /= bodyComp.RequiredLegs;
        sprintSpeed /= bodyComp.RequiredLegs;
        acceleration /= bodyComp.RequiredLegs;

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
