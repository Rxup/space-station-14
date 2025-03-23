﻿using System.Linq;
using Content.Shared.Backmen.Surgery.Traumas.Components;
using Content.Shared.Backmen.Surgery.Wounds.Components;
using Content.Shared.Body.Components;
using Content.Shared.Body.Part;
using Content.Shared.FixedPoint;
using Content.Shared.Movement.Components;
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
            case BoneSeverity.Normal:
                if (bodyComp.PartType == BodyPartType.Hand)
                {
                    _virtual.DeleteInHandsMatching(bodyComp.Body.Value, bone);
                }

                break;

            case BoneSeverity.Damaged:
                _audio.PlayPvs(bone.Comp.BoneBreakSound, bodyComp.Body.Value, AudioParams.Default.WithVolume(-8f));
                break;

            case BoneSeverity.Broken:
                // TODO: _audio.PlayPvs(bone.Comp.BoneDestroyedSound, bodyComp.Body.Value, AudioParams.Default.WithVolume(12f));

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

    public bool ApplyBoneTrauma(
        EntityUid boneEnt,
        Entity<WoundableComponent> woundable,
        Entity<TraumaInflicterComponent> inflicter,
        FixedPoint2 inflicterSeverity,
        BoneComponent? boneComp = null)
    {
        if (!Resolve(boneEnt, ref boneComp))
            return false;

        AddTrauma(boneEnt, woundable, inflicter, TraumaType.BoneDamage, inflicterSeverity);
        ApplyDamageToBone(boneEnt, inflicterSeverity, boneComp);

        return true;
    }

    public bool SetBoneIntegrity(EntityUid bone, FixedPoint2 integrity, BoneComponent? boneComp = null)
    {
        if (!Resolve(bone, ref boneComp))
            return false;

        var newIntegrity = FixedPoint2.Clamp(integrity, 0, boneComp.IntegrityCap);
        if (boneComp.BoneIntegrity == newIntegrity)
            return false;

        var ev = new BoneIntegrityChangedEvent((bone, boneComp), boneComp.BoneIntegrity, newIntegrity);
        RaiseLocalEvent(bone, ref ev);

        boneComp.BoneIntegrity = newIntegrity;
        CheckBoneSeverity(bone, boneComp);

        Dirty(bone, boneComp);
        return true;
    }

    public bool ApplyDamageToBone(EntityUid bone, FixedPoint2 severity, BoneComponent? boneComp = null)
    {
        if (!Resolve(bone, ref boneComp))
            return false;

        var newIntegrity = FixedPoint2.Clamp(boneComp.BoneIntegrity - severity, 0, boneComp.IntegrityCap);
        if (boneComp.BoneIntegrity == newIntegrity)
            return false;

        var ev = new BoneIntegrityChangedEvent((bone, boneComp), boneComp.BoneIntegrity, newIntegrity);
        RaiseLocalEvent(bone, ref ev);

        boneComp.BoneIntegrity = newIntegrity;
        CheckBoneSeverity(bone, boneComp);

        Dirty(bone, boneComp);
        return true;
    }

    #endregion

    #region Private API

    private void CheckBoneSeverity(EntityUid bone, BoneComponent boneComp)
    {
        var nearestSeverity = boneComp.BoneSeverity;

        foreach (var (severity, value) in _boneThresholds.OrderByDescending(kv => kv.Value))
        {
            if (boneComp.BoneIntegrity < value)
                continue;

            nearestSeverity = severity;
            break;
        }

        if (nearestSeverity != boneComp.BoneSeverity)
        {
            var ev = new BoneSeverityChangedEvent((bone, boneComp), boneComp.BoneSeverity, nearestSeverity);
            RaiseLocalEvent(bone, ref ev, true);

            // TODO: Move this to BoneSeverityChangedEvent handler

        }
        boneComp.BoneSeverity = nearestSeverity;

        Dirty(bone, boneComp);
    }

    private void ProcessLegsState(EntityUid body)
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

            if (!TryComp<BoneComponent>(legWoundable.Bone!.ContainedEntities[0], out var boneComp))
                continue;

            // get the foot penalty
            var penalty = 1f;
            var footEnt =
                _body.GetBodyChildrenOfType(body,
                        BodyPartType.Foot,
                        symmetry: Comp<BodyPartComponent>(legEntity).Symmetry)
                    .FirstOrNull();

            if (footEnt != null)
            {
                if (TryComp<BoneComponent>(legWoundable.Bone!.ContainedEntities[0], out var footBone))
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
                penalty = 0.44f;
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
        if (walkSpeed < rawWalkSpeed / 3.4)
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
