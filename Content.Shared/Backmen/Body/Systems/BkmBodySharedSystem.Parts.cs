using Content.Shared.Body;
using Content.Shared.Body.Part;
using Content.Shared.Inventory;
using Content.Shared.Movement.Components;
using Robust.Shared.Containers;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Shared.Backmen.Targeting;
using Robust.Shared.Random;

namespace Content.Shared.Backmen.Body.Systems;

public partial class BkmBodySharedSystem
{
    private void InitializeParts()
    {
        // Chest cavity ItemSlot on leftover BodyPartComponent item prototypes.
        SubscribeLocalEvent<BodyPartComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<BodyPartComponent, ComponentRemove>(OnBodyPartRemove);
    }

    private void OnMapInit(Entity<BodyPartComponent> ent, ref MapInitEvent args)
    {
        if (ent.Comp.PartType != BodyPartType.Chest)
            return;

        _slots.AddItemSlot(ent, ent.Comp.ContainerName, ent.Comp.ItemInsertionSlot);
        Dirty(ent, ent.Comp);
    }

    private void OnBodyPartRemove(Entity<BodyPartComponent> ent, ref ComponentRemove args)
    {
        if (ent.Comp.PartType == BodyPartType.Chest)
            _slots.RemoveItemSlot(ent, ent.Comp.ItemInsertionSlot);
    }

    /// <summary>
    /// Drops inventory items when the body loses its last part of a given type.
    /// </summary>
    public void DropSlotContents(EntityUid body, BodyPartType partType)
    {
        if (!TryComp<InventoryComponent>(body, out var inventory)
            || GetBodyPartCount(body, partType) != 1
            || !TryGetPartSlotContainerName(partType, out var containerNames))
            return;

        foreach (var containerName in containerNames)
        {
            InventorySystem.DropSlotContents(body, containerName, inventory);
        }
    }

    #region Misc

    /// <summary>
    /// Crawl speed factor when all leg organs are missing (matches foot-amputation penalty in trauma).
    /// </summary>
    private const float LeglessSpeedFactor = 0.22f;

    public void UpdateMovementSpeed(
        EntityUid bodyId,
        BodyComponent? body = null,
        MovementSpeedModifierComponent? movement = null)
    {
        if (!Resolve(bodyId, ref body, ref movement, logMissing: false))
            return;

        var requiredLegs = GetEffectiveRequiredLegs(bodyId, body);
        if (requiredLegs <= 0)
            return;

        float walkSpeed;
        float sprintSpeed;
        float acceleration;

        if (body.LegEntities.Count == 0)
        {
            walkSpeed = MovementSpeedModifierComponent.DefaultBaseWalkSpeed * LeglessSpeedFactor;
            sprintSpeed = MovementSpeedModifierComponent.DefaultBaseSprintSpeed * LeglessSpeedFactor;
            acceleration = MovementSpeedModifierComponent.DefaultAcceleration * LeglessSpeedFactor;
        }
        else
        {
            walkSpeed = 0f;
            sprintSpeed = 0f;
            acceleration = 0f;

            foreach (var legEntity in body.LegEntities)
            {
                if (TryComp<MovementBodyPartComponent>(legEntity, out var legModifier))
                {
                    walkSpeed += legModifier.WalkSpeed;
                    sprintSpeed += legModifier.SprintSpeed;
                    acceleration += legModifier.Acceleration;
                }
                else
                {
                    walkSpeed += MovementSpeedModifierComponent.DefaultBaseWalkSpeed;
                    sprintSpeed += MovementSpeedModifierComponent.DefaultBaseSprintSpeed;
                    acceleration += MovementSpeedModifierComponent.DefaultAcceleration;
                }
            }

            walkSpeed /= requiredLegs;
            sprintSpeed /= requiredLegs;
            acceleration /= requiredLegs;
        }

        Movement.ChangeBaseSpeed(bodyId, walkSpeed, sprintSpeed, acceleration, movement);
        Movement.RefreshMovementSpeedModifiers(bodyId, movement);
    }

    public TargetBodyPart? GetRandomBodyPart(EntityUid target,
        EntityUid attacker,
        TargetingComponent? targetComp = null,
        TargetingComponent? attackerComp = null)
    {
        if (_targeting.TryResolveCombatBodyPart(target, attacker, null, out var hitPart))
            return hitPart;

        return GetRandomBodyPart(target);
    }

    public TargetBodyPart? GetRandomBodyPart(EntityUid target,
        TargetBodyPart targetPart = TargetBodyPart.Chest,
        TargetingComponent? targetComp = null)
    {
        if (_targeting.TryGetCombatTargetOddsSpread("Default", targetPart, out var weights))
            return _targeting.PickCombatBodyPart(target, weights, null);

        return targetPart;
    }

    public TargetBodyPart? GetRandomBodyPart(EntityUid target)
    {
        var toPick = GetWoundableTargets(target).ToList();
        if (toPick.Count == 0)
            return null;

        var picked = _random.PickAndTake(toPick);

        if (TryComp<BodyPartComponent>(picked, out var part))
            return GetTargetBodyPart(part);

        if (TryComp<OrganComponent>(picked, out var organ)
            && organ.Category is { } category
            && TargetBodyPartMapping.TryGetTargetPart(category, out var targetPart))
            return targetPart;

        return null;
    }

    public TargetBodyPart? GetTargetBodyPart(Entity<BodyPartComponent> part)
    {
        return GetTargetBodyPart(part.Comp.PartType, part.Comp.Symmetry);
    }

    public TargetBodyPart? GetTargetBodyPart(BodyPartComponent part)
    {
        return GetTargetBodyPart(part.PartType, part.Symmetry);
    }

    /// <summary>
    /// Converts Enums from BodyPartType to their Targeting system equivalent.
    /// </summary>
    public TargetBodyPart? GetTargetBodyPart(BodyPartType type, BodyPartSymmetry symmetry)
    {
        return (type, symmetry) switch
        {
            (BodyPartType.Head, _) => TargetBodyPart.Head,
            (BodyPartType.Chest, _) => TargetBodyPart.Chest,
            (BodyPartType.Groin, _) => TargetBodyPart.Chest,
            (BodyPartType.Arm, BodyPartSymmetry.Left) => TargetBodyPart.LeftArm,
            (BodyPartType.Arm, BodyPartSymmetry.Right) => TargetBodyPart.RightArm,
            (BodyPartType.Hand, BodyPartSymmetry.Left) => TargetBodyPart.LeftHand,
            (BodyPartType.Hand, BodyPartSymmetry.Right) => TargetBodyPart.RightHand,
            (BodyPartType.Leg, BodyPartSymmetry.Left) => TargetBodyPart.LeftLeg,
            (BodyPartType.Leg, BodyPartSymmetry.Right) => TargetBodyPart.RightLeg,
            (BodyPartType.Foot, BodyPartSymmetry.Left) => TargetBodyPart.LeftFoot,
            (BodyPartType.Foot, BodyPartSymmetry.Right) => TargetBodyPart.RightFoot,
            _ => null,
        };
    }

    /// <summary>
    /// Converts Enums from Targeting system to their BodyPartType equivalent.
    /// </summary>
    public (BodyPartType Type, BodyPartSymmetry Symmetry) ConvertTargetBodyPart(TargetBodyPart? targetPart)
    {
        targetPart = targetPart == null ? null : TargetBodyPartMapping.Normalize(targetPart.Value);

        return targetPart switch
        {
            TargetBodyPart.Head => (BodyPartType.Head, BodyPartSymmetry.None),
            TargetBodyPart.Chest => (BodyPartType.Chest, BodyPartSymmetry.None),
            TargetBodyPart.LeftArm => (BodyPartType.Arm, BodyPartSymmetry.Left),
            TargetBodyPart.LeftHand => (BodyPartType.Hand, BodyPartSymmetry.Left),
            TargetBodyPart.RightArm => (BodyPartType.Arm, BodyPartSymmetry.Right),
            TargetBodyPart.RightHand => (BodyPartType.Hand, BodyPartSymmetry.Right),
            TargetBodyPart.LeftLeg => (BodyPartType.Leg, BodyPartSymmetry.Left),
            TargetBodyPart.LeftFoot => (BodyPartType.Foot, BodyPartSymmetry.Left),
            TargetBodyPart.RightLeg => (BodyPartType.Leg, BodyPartSymmetry.Right),
            TargetBodyPart.RightFoot => (BodyPartType.Foot, BodyPartSymmetry.Right),
            _ => (BodyPartType.Chest, BodyPartSymmetry.None)
        };
    }

    #endregion

    #region Queries

    /// <summary>
    /// Returns true if the bodyId has any woundable organs of this part type.
    /// </summary>
    public bool BodyHasPartType(
        EntityUid bodyId,
        BodyPartType type,
        BodyComponent? body = null)
    {
        return GetBodyPartCount(bodyId, type, body) > 0;
    }

    /// <summary>
    ///     Tries to get a list of ValueTuples of EntityUid and OrganComponent on each organ
    ///     on the given body that has the specified component type.
    /// </summary>
    public bool TryGetBodyPartOrgans(
        EntityUid uid,
        Type type,
        [NotNullWhen(true)] out List<(EntityUid Id, OrganComponent Organ)>? organs,
        BodyPartComponent? part = null)
    {
        if (TryComp<BodyComponent>(uid, out var bodyComp))
        {
            var flat = new List<(EntityUid Id, OrganComponent Organ)>();
            foreach (var organ in GetBodyOrgans(uid, bodyComp))
            {
                if (HasComp(organ.Id, type))
                    flat.Add((organ.Id, organ.Component));
            }

            if (flat.Count != 0)
            {
                organs = flat;
                return true;
            }
        }

        organs = null;
        return false;
    }

    public bool TryGetPartSlotContainerName(BodyPartType partType, out HashSet<string> containerNames)
    {
        containerNames = partType switch
        {
            BodyPartType.Hand => ["gloves"],
            BodyPartType.Foot => ["shoes"],
            BodyPartType.Head => ["eyes", "ears", "head", "mask"],
            _ => [],
        };
        return containerNames.Count > 0;
    }

    public bool TryGetPartFromSlotContainer(string slot, out BodyPartType? partType)
    {
        partType = slot switch
        {
            "gloves" => BodyPartType.Hand,
            "shoes" or "socks" => BodyPartType.Foot,
            "eyes" or "ears" or "head" or "mask" => BodyPartType.Head,
            _ => null,
        };
        return partType is not null;
    }

    /// <summary>
    /// Body part required to equip an inventory slot.
    /// </summary>
    public bool TryGetRequiredBodyPartForSlot(string slot, out BodyPartType partType)
    {
        if (!TryGetPartFromSlotContainer(slot, out var mapped) || mapped is not { } mappedPart)
        {
            partType = default;
            return false;
        }

        partType = mappedPart;
        return true;
    }

    public int GetBodyPartCount(EntityUid bodyId, BodyPartType partType, BodyComponent? body = null)
    {
        if (!Resolve(bodyId, ref body, logMissing: false))
            return 0;

        var count = 0;
        foreach (var target in GetWoundableTargets(bodyId, body))
        {
            if (TerminatingOrDeleted(target))
                continue;

            if (!TryComp<OrganComponent>(target, out var organ)
                || organ.Category is not { } category
                || !SurgeryBodyPartMapping.TryGetBodyPartType(category, out var type, out _)
                || type != partType)
                continue;

            count++;
        }

        return count;
    }

    #endregion
}
