using Content.Shared.Body;
using Content.Shared.Body.Events;
using Content.Shared.Body.Part;
using Content.Shared.Inventory;
using Content.Shared.Movement.Components;
using Content.Shared.Standing;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using Content.Shared.Backmen.Surgery.Body.Events;
using Content.Shared.Backmen.Targeting;
using Content.Shared.Backmen.Surgery.Body.Organs;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Robust.Shared.Random;

namespace Content.Shared.Backmen.Body.Systems;

public partial class BkmBodySharedSystem
{
    private static readonly ProtoId<DamageTypePrototype> BloodlossDamageType = "Bloodloss";
    private void InitializeParts()
    {
        // TODO: This doesn't handle comp removal on child ents.

        // If you modify this also see the Body partial for root parts.
        SubscribeLocalEvent<BodyPartComponent, EntInsertedIntoContainerMessage>(OnBodyPartInserted);
        SubscribeLocalEvent<BodyPartComponent, EntRemovedFromContainerMessage>(OnBodyPartRemoved);

        // Shitmed Change Start
        SubscribeLocalEvent<BodyPartComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<BodyPartComponent, ComponentRemove>(OnBodyPartRemove);
    }

    private void OnMapInit(Entity<BodyPartComponent> ent, ref MapInitEvent args)
    {
        if (ent.Comp.PartType == BodyPartType.Chest)
        {
            _slots.AddItemSlot(ent, ent.Comp.ContainerName, ent.Comp.ItemInsertionSlot);
            Dirty(ent, ent.Comp);
        }

        if (ent.Comp.OnAdd is not null || ent.Comp.OnRemove is not null)
            EnsureComp<BodyPartEffectComponent>(ent);

        foreach (var connection in ent.Comp.Children.Keys)
        {
            Containers.EnsureContainer<ContainerSlot>(ent, GetPartSlotContainerId(connection));
        }
    }

    private void OnBodyPartRemove(Entity<BodyPartComponent> ent, ref ComponentRemove args)
    {
        if (ent.Comp.PartType == BodyPartType.Chest)
            _slots.RemoveItemSlot(ent, ent.Comp.ItemInsertionSlot);
    }

    /// <summary>
    ///     Shitmed Change: This function handles dropping the items in an entity's slots if they lose all of a given part.
    ///     Such as their hands, feet, head, etc.
    /// </summary>
    public void DropSlotContents(Entity<BodyPartComponent> partEnt)
    {
        if (partEnt.Comp.Body is null
            || !TryComp<InventoryComponent>(partEnt.Comp.Body, out var inventory) || // Prevent error for non-humanoids
            GetBodyPartCount(partEnt.Comp.Body.Value, partEnt.Comp.PartType) != 1
            || !TryGetPartSlotContainerName(partEnt.Comp.PartType, out var containerNames))
            return;

        foreach (var containerName in containerNames)
        {
            InventorySystem.DropSlotContents(partEnt.Comp.Body.Value, containerName, inventory);
        }

    }

    // Shitmed Change End
    private void OnBodyPartInserted(Entity<BodyPartComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        // Body part inserted into another body part.
        var insertedUid = args.Entity;
        var slotId = args.Container.ID;

        var body = ent.Comp.Body;
        if (body is null)
            return;

        if (TryComp(insertedUid, out BodyPartComponent? part) && slotId.Contains(PartSlotContainerIdPrefix + GetSlotFromBodyPart(part))) // Shitmed Change
        {
            AddPart(body.Value, (insertedUid, part), slotId);
            RecursiveBodyUpdate((insertedUid, part), body.Value);
        }
#if DEBUG
        else if(HasComp<BodyPartComponent>(insertedUid))
        {
            DebugTools.Assert(
                slotId.Contains(PartSlotContainerIdPrefix + GetSlotFromBodyPart(part)),
                $"BodyPartComponent has not been inserted ({Prototype(args.Entity)?.ID}) into {Prototype(ent.Comp.Body!.Value)?.ID}" +
                $" прототип должен иметь подключение начиная с {GetSlotFromBodyPart(part)} (сейчас {slotId.Replace(PartSlotContainerIdPrefix,"")})");
        }
#endif

        if (TryComp(insertedUid, out OrganComponent? organ) && slotId.Contains(OrganSlotContainerIdPrefix + organ.SlotId.ToLower(CultureInfo.InvariantCulture))) // Shitmed Change
        {
            AddOrgan((insertedUid, organ), body.Value, ent);
        }
#if DEBUG
        else if(HasComp<OrganComponent>(insertedUid))
        {
            DebugTools.Assert($"OrganComponent has not been inserted ({Prototype(args.Entity)?.ID}) into {Prototype(ent.Comp.Body!.Value)?.ID}");
        }
#endif
    }

    private void OnBodyPartRemoved(Entity<BodyPartComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        // I hate shitmed I hate shitmed I hate shitmed I hate shitmed I hate shitmed I hate shitmed I hate shitmed I hate shitmed I hate shitmed I hate shitmed
        // I hate shitmed I hate shitmed I hate shitmed I hate shitmed I hate shitmed I hate shitmed I hate shitmed I hate shitmed I hate shitmed I hate shitmed
        if (TerminatingOrDeleted(ent.Comp.Body))
            return;

        // Body part removed from another body part.
        var removedUid = args.Entity;
        var slotId = args.Container.ID;

        // Shitmed Change Start
        if (TryComp(removedUid, out BodyPartComponent? part))
        {
            if (!slotId.Contains(PartSlotContainerIdPrefix + GetSlotFromBodyPart(part)))
                return;

            DebugTools.Assert(part.Body == ent.Comp.Body);

            if (part.Body is not null)
            {
                RemovePart(part.Body.Value, (removedUid, part), slotId);
                RecursiveBodyUpdate((removedUid, part), null);
            }
        }

        if (TryComp(removedUid, out OrganComponent? organ))
        {
            if (!slotId.Contains(OrganSlotContainerIdPrefix + organ.SlotId))
                return;

            DebugTools.Assert(organ.Body == ent.Comp.Body);

            RemoveOrgan((removedUid, organ), ent);
        }
        // Shitmed Change End
    }

    private void RecursiveBodyUpdate(Entity<BodyPartComponent> ent, EntityUid? bodyUid)
    {
        ent.Comp.Body = bodyUid;
        Dirty(ent, ent.Comp);

        foreach (var slotId in ent.Comp.Organs.Keys)
        {
            if (!Containers.TryGetContainer(ent, GetOrganContainerId(slotId), out var container))
                continue;

            foreach (var organ in container.ContainedEntities)
            {
                if (!TryComp(organ, out OrganComponent? organComp))
                    continue;

                if (organComp.Body is { Valid: true } oldBodyUid)
                {
                    var removedEv = new OrganRemovedFromBodyEvent(oldBodyUid, ent);
                    RaiseLocalEvent(organ, ref removedEv);
                }

                organComp.Body = bodyUid;
                organComp.BodyPart = ent.Owner;
                if (bodyUid is not null)
                {
                    var addedEv = new OrganAddedToBodyEvent(bodyUid.Value, ent);
                    RaiseLocalEvent(organ, ref addedEv);
                }

                Dirty(organ, organComp);
            }
        }

        // The code for RemovePartEffect() should live here, because it literally is the point of this recursive function.
        // But the debug asserts at the top plus existing tests need refactoring for this. So we'll be lazy.
        foreach (var slotId in ent.Comp.Children.Keys)
        {
            if (!Containers.TryGetContainer(ent, GetPartSlotContainerId(slotId), out var container))
                continue;

            foreach (var containedUid in container.ContainedEntities)
            {
                if (TryComp(containedUid, out BodyPartComponent? childPart))
                    RecursiveBodyUpdate((containedUid, childPart), bodyUid);
            }
        }
    }

    protected virtual void AddPart(
        Entity<BodyComponent?> bodyEnt,
        Entity<BodyPartComponent> partEnt,
        string slotId)
    {
        Dirty(partEnt, partEnt.Comp);
        partEnt.Comp.Body = bodyEnt;

        var ev = new BodyPartAddedEvent(slotId, partEnt);
        RaiseLocalEvent(bodyEnt, ref ev);

        var ev1 = new BodyPartAddedEvent(slotId, partEnt);
        RaiseLocalEvent(partEnt, ref ev1);

        AddLeg(partEnt, bodyEnt);
    }

    protected virtual void RemovePart(
        Entity<BodyComponent?> bodyEnt,
        Entity<BodyPartComponent> partEnt,
        string slotId)
    {
        Resolve(bodyEnt, ref bodyEnt.Comp, logMissing: false);
        Dirty(partEnt, partEnt.Comp);

        var ev = new BodyPartRemovedEvent(slotId, partEnt);
        RaiseLocalEvent(bodyEnt, ref ev);

        var ev1 = new BodyPartRemovedEvent(slotId, partEnt);
        RaiseLocalEvent(partEnt, ref ev1);

        RemoveLeg(partEnt, bodyEnt);
        PartRemoveDamage(bodyEnt, partEnt);
    }

    private void AddLeg(Entity<BodyPartComponent> legEnt, Entity<BodyComponent?> bodyEnt)
    {
        if (!Resolve(bodyEnt, ref bodyEnt.Comp, logMissing: false))
            return;

        if (legEnt.Comp.PartType != BodyPartType.Leg)
            return;

        bodyEnt.Comp.LegEntities.Add(legEnt);
        UpdateMovementSpeed(bodyEnt);
        Dirty(bodyEnt, bodyEnt.Comp);
    }

    private void RemoveLeg(Entity<BodyPartComponent> legEnt, Entity<BodyComponent?> bodyEnt)
    {
        if (!Resolve(bodyEnt, ref bodyEnt.Comp, logMissing: false))
            return;

        if (legEnt.Comp.PartType != BodyPartType.Leg)
            return;

        bodyEnt.Comp.LegEntities.Remove(legEnt);
        UpdateMovementSpeed(bodyEnt);
        Dirty(bodyEnt, bodyEnt.Comp);

        if (bodyEnt.Comp.LegEntities.Count != 0)
            return;

        if (!TryComp<StandingStateComponent>(bodyEnt, out var standingState)
            || !standingState.Standing
            || !Standing.Down(bodyEnt, standingState: standingState))
            return;

        var ev = new DropHandItemsEvent();
        RaiseLocalEvent(bodyEnt, ref ev);
    }

    private void PartRemoveDamage(Entity<BodyComponent?> bodyEnt, Entity<BodyPartComponent> partEnt)
    {
        if (!Resolve(bodyEnt, ref bodyEnt.Comp, logMissing: false))
            return;

        if (!_timing.ApplyingState
            && partEnt.Comp.IsVital
            && !GetBodyChildrenOfType(bodyEnt, partEnt.Comp.PartType, bodyEnt.Comp).Any()
           )
        {
            // TODO BODY SYSTEM KILL : remove this when wounding and required parts are implemented properly
            var damage = new DamageSpecifier(Prototypes.Index(BloodlossDamageType), 300);
            Damageable.ChangeDamage(bodyEnt.Owner, damage);
        }
    }

    /// <summary>
    /// Tries to get the parent body part to this if applicable.
    /// Doesn't validate if it's a part of body system.
    /// </summary>
    public EntityUid? GetParentPartOrNull(EntityUid uid)
    {
        if (!Containers.TryGetContainingContainer((uid, null, null), out var container))
            return null;

        var parent = container.Owner;

        if (!HasComp<BodyPartComponent>(parent))
            return null;

        return parent;
    }

    /// <summary>
    /// Tries to get the parent body part and slot to this if applicable.
    /// </summary>
    public (EntityUid Parent, string Slot)? GetParentPartAndSlotOrNull(EntityUid uid)
    {
        if (!Containers.TryGetContainingContainer((uid, null, null), out var container))
            return null;

        var slotId = GetPartSlotContainerIdFromContainer(container.ID);

        if (string.IsNullOrEmpty(slotId))
            return null;

        var parent = container.Owner;

        if (!TryComp<BodyPartComponent>(parent, out var parentBody)
            || !parentBody.Children.ContainsKey(slotId))
            return null;

        return (parent, slotId);
    }

    /// <summary>
    /// Tries to get the relevant parent body part to this if it exists.
    /// It won't exist if this is the root body part or if it's not in a body.
    /// </summary>
    public bool TryGetParentBodyPart(
        EntityUid partUid,
        [NotNullWhen(true)] out EntityUid? parentUid,
        [NotNullWhen(true)] out BodyPartComponent? parentComponent)
    {
        DebugTools.Assert(HasComp<BodyPartComponent>(partUid));
        parentUid = null;
        parentComponent = null;

        if (Containers.TryGetContainingContainer((partUid, null, null), out var container) &&
            TryComp(container.Owner, out parentComponent))
        {
            parentUid = container.Owner;
            return true;
        }

        return false;
    }

    #region Slots

    /// <summary>
    /// Creates a BodyPartSlot on the specified partUid.
    /// </summary>
    private BodyPartSlot? CreatePartSlot(
        EntityUid partUid,
        string slotId,
        BodyPartType partType,
        BodyPartSymmetry symmetry = BodyPartSymmetry.None,
        BodyPartComponent? part = null)
    {
        if (!Resolve(partUid, ref part, logMissing: false))
            return null;

        Containers.EnsureContainer<ContainerSlot>(partUid, GetPartSlotContainerId(slotId));
        var partSlot = new BodyPartSlot(slotId, partType, symmetry);
        part.Children.Add(slotId, partSlot);
        Dirty(partUid, part);
        return partSlot;
    }

    /// <summary>
    /// Tries to create a BodyPartSlot on the specified partUid.
    /// </summary>
    /// <returns>false if not relevant or can't add it.</returns>
    public bool TryCreatePartSlot(
        EntityUid? partId,
        string slotId,
        BodyPartType partType,
        [NotNullWhen(true)] out BodyPartSlot? slot,
        BodyPartSymmetry symmetry = BodyPartSymmetry.None,
        BodyPartComponent? part = null)
    {
        slot = null;

        if (partId is null
            || !Resolve(partId.Value, ref part, logMissing: false))
        {
            return false;
        }

        Containers.EnsureContainer<ContainerSlot>(partId.Value, GetPartSlotContainerId(slotId));
        slot = new BodyPartSlot(slotId, partType, symmetry);

        if (!part.Children.TryAdd(slotId, slot.Value))
            return false;

        Dirty(partId.Value, part);
        return true;
    }

    public bool TryCreatePartSlotAndAttach(
        EntityUid parentId,
        string slotId,
        EntityUid childId,
        BodyPartType partType,
        BodyPartSymmetry symmetry = BodyPartSymmetry.None,
        BodyPartComponent? parent = null,
        BodyPartComponent? child = null)
    {
        return TryCreatePartSlot(parentId, slotId, partType, out _, symmetry, parent)
               && AttachPart(parentId, slotId, childId, parent, child);
    }

    #endregion

    #region RootPartManagement

    /// <summary>
    /// Returns true if the partId is the root body container for the specified bodyId.
    /// </summary>
    public bool IsPartRoot(
        EntityUid bodyId,
        EntityUid partId,
        BodyComponent? body = null,
        BodyPartComponent? part = null)
    {
        return Resolve(partId, ref part)
            && Resolve(bodyId, ref body)
            && Containers.TryGetContainingContainer(bodyId, partId, out var container)
            && container.ID == BodyRootContainerId;
    }

    /// <summary>
    /// Returns true if we can attach the partId to the bodyId as the root entity.
    /// </summary>
    public bool CanAttachToRoot(
        EntityUid bodyId,
        EntityUid partId,
        BodyComponent? body = null,
        BodyPartComponent? part = null)
    {
        return false;
    }

    /// <summary>
    /// Returns the root part of this body if it exists.
    /// </summary>
    public (EntityUid Entity, BodyPartComponent BodyPart)? GetRootPartOrNull(EntityUid bodyId, BodyComponent? body = null) =>
        null;

    /// <summary>
    /// Returns true if the partId can be attached to the parentId in the specified slot.
    /// </summary>
    public bool CanAttachPart(
        EntityUid parentId,
        BodyPartSlot slot,
        EntityUid partId,
        BodyPartComponent? parentPart = null,
        BodyPartComponent? part = null)
    {
        return Resolve(partId, ref part, logMissing: false)
            && Resolve(parentId, ref parentPart, logMissing: false)
            && CanAttachPart(parentId, slot.Id, partId, parentPart, part);
    }

    /// <summary>
    /// Returns true if we can attach the specified partId to the parentId in the specified slot.
    /// </summary>
    public bool CanAttachPart(
        EntityUid parentId,
        string slotId,
        EntityUid partId,
        BodyPartComponent? parentPart = null,
        BodyPartComponent? part = null)
    {
        return Resolve(partId, ref part, logMissing: false)
            && Resolve(parentId, ref parentPart, logMissing: false)
            && parentPart.Children.TryGetValue(slotId, out var parentSlotData)
            && part.PartType == parentSlotData.Type
            && Containers.TryGetContainer(parentId, GetPartSlotContainerId(slotId), out var container)
            && Containers.CanInsert(partId, container);
    }

    public bool AttachPartToRoot(
        EntityUid bodyId,
        EntityUid partId,
        BodyComponent? body = null,
        BodyPartComponent? part = null) =>
        false;

    /// <summary>
    /// Detaches the root body part from a legacy hierarchical body.
    /// </summary>
    public bool DetachPartFromRoot(
        EntityUid bodyId,
        EntityUid partId,
        BodyComponent? body = null,
        BodyPartComponent? part = null) =>
        false;

    /// <summary>
    ///     Returns true if this parentId supports attaching a new part to the specified slot.
    /// </summary>
    public bool CanAttachToSlot(
        EntityUid parentId,
        string slotId,
        BodyPartComponent? parentPart = null)
    {
        return Resolve(parentId, ref parentPart, logMissing: false)
               && parentPart.Children.ContainsKey(slotId);
    }

    // backmen edit start
    /// <summary>
    /// Returns true if the partId can be detached from the parentId in the specified slot.
    /// </summary>
    public bool CanDetachPart(
        EntityUid parentId,
        BodyPartSlot slot,
        EntityUid partId,
        BodyPartComponent? parentPart = null,
        BodyPartComponent? part = null)
    {
        return Resolve(partId, ref part, logMissing: false)
               && Resolve(parentId, ref parentPart, logMissing: false)
               && CanDetachPart(parentId, slot.Id, partId, parentPart, part);
    }

    /// <summary>
    /// Returns true if we can detach the specified partId from the parentId in the specified slot.
    /// </summary>
    public bool CanDetachPart(
        EntityUid parentId,
        string slotId,
        EntityUid partId,
        BodyPartComponent? parentPart = null,
        BodyPartComponent? part = null)
    {
        return Resolve(partId, ref part, logMissing: false)
               && Resolve(parentId, ref parentPart, logMissing: false)
               && parentPart.Children.TryGetValue(slotId, out var parentSlotData)
               && part.PartType == parentSlotData.Type
               && Containers.TryGetContainer(parentId, GetPartSlotContainerId(slotId), out var container)
               && Containers.CanRemove(partId, container);
    }
    // backmen edit end

    #endregion

    #region Attach/Detach

    /// <summary>
    /// Attaches a body part to the specified body part parent.
    /// </summary>
    public bool AttachPart(
        EntityUid parentPartId,
        string slotId,
        EntityUid partId,
        BodyPartComponent? parentPart = null,
        BodyPartComponent? part = null)
    {
        return Resolve(parentPartId, ref parentPart, logMissing: false)
            && parentPart.Children.TryGetValue(slotId, out var slot)
            && AttachPart(parentPartId, slot, partId, parentPart, part);
    }

    /// <summary>
    /// Attaches a body part to the specified body part parent.
    /// </summary>
    public bool AttachPart(
        EntityUid parentPartId,
        BodyPartSlot slot,
        EntityUid partId,
        BodyPartComponent? parentPart = null,
        BodyPartComponent? part = null)
    {
        if (!Resolve(parentPartId, ref parentPart, logMissing: false)
            || !Resolve(partId, ref part, logMissing: false)
            || !CanAttachPart(parentPartId, slot.Id, partId, parentPart, part)
            || !parentPart.Children.ContainsKey(slot.Id))
        {
            return false;
        }

        if (!Containers.TryGetContainer(parentPartId, GetPartSlotContainerId(slot.Id), out var container))
        {
            DebugTools.Assert($"Unable to find body slot {slot.Id} for {ToPrettyString(parentPartId)}");
            return false;
        }

        part.ParentSlot = slot;

        return Containers.Insert(partId, container);
    }

    // backmen edit start
    /// <summary>
    /// Detaches a body part from the specified body part parent.
    /// </summary>
    public bool DetachPart(
        EntityUid parentPartId,
        string slotId,
        EntityUid partId,
        BodyPartComponent? parentPart = null,
        BodyPartComponent? part = null)
    {
        return Resolve(parentPartId, ref parentPart, logMissing: false)
               && parentPart.Children.TryGetValue(slotId, out var slot)
               && DetachPart(parentPartId, slot, partId, parentPart, part);
    }

    /// <summary>
    /// Detaches a body part from the specified body part parent.
    /// </summary>
    public bool DetachPart(
        EntityUid parentPartId,
        BodyPartSlot slot,
        EntityUid partId,
        BodyPartComponent? parentPart = null,
        BodyPartComponent? part = null)
    {
        if (!Resolve(parentPartId, ref parentPart, logMissing: false)
            || !Resolve(partId, ref part, logMissing: false)
            || !CanDetachPart(parentPartId, slot.Id, partId, parentPart, part)
            || !parentPart.Children.ContainsKey(slot.Id))
        {
            return false;
        }

        if (!Containers.TryGetContainer(parentPartId, GetPartSlotContainerId(slot.Id), out var container))
        {
            DebugTools.Assert($"Unable to find body slot {slot.Id} for {ToPrettyString(parentPartId)}");
            return false;
        }

        //parentPart.Children.Remove(slot.Id);

        return Containers.Remove(partId, container, destination: Transform(part.Body!.Value).Coordinates);
    }
    // backmen edit end

    #endregion

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
    /// Get all organs for the specified body part.
    /// </summary>
    public IEnumerable<(EntityUid Id, OrganComponent Component)> GetPartOrgans(EntityUid partId, BodyPartComponent? part = null)
    {
        if (!Resolve(partId, ref part, logMissing: false))
            yield break;

        foreach (var containerSlotId in part.Organs.Keys.Select(GetOrganContainerId))
        {
            if (!Containers.TryGetContainer(partId, containerSlotId, out var container))
                continue;

            foreach (var containedEnt in container.ContainedEntities.ToArray())
            {
                if (!TryComp(containedEnt, out OrganComponent? organ))
                    continue;

                yield return (containedEnt, organ);
            }
        }
    }

    /// <summary>
    /// Gets all BaseContainers for body parts on this entity and its child entities.
    /// </summary>
    public IEnumerable<BaseContainer> GetPartContainers(EntityUid id, BodyPartComponent? part = null)
    {
        if (!Resolve(id, ref part, logMissing: false) ||
            part.Children.Count == 0)
        {
            yield break;
        }

        foreach (var slotId in part.Children.Keys)
        {
            var containerSlotId = GetPartSlotContainerId(slotId);

            if (!Containers.TryGetContainer(id, containerSlotId, out var container))
                continue;

            yield return container;

            foreach (var ent in container.ContainedEntities)
            {
                foreach (var childContainer in GetPartContainers(ent))
                {
                    yield return childContainer;
                }
            }
        }
    }

    /// <summary>
    /// Returns all body part components for this entity including itself.
    /// </summary>
    public IEnumerable<(EntityUid Id, BodyPartComponent Component)> GetBodyPartChildren(
        EntityUid partId,
        BodyPartComponent? part = null)
    {
        if (!Resolve(partId, ref part, logMissing: false))
            yield break;

        yield return (partId, part);

        foreach (var slotId in part.Children.Keys)
        {
            var containerSlotId = GetPartSlotContainerId(slotId);

            if (Containers.TryGetContainer(partId, containerSlotId, out var container))
            {
                foreach (var containedEnt in container.ContainedEntities)
                {
                    if (!TryComp(containedEnt, out BodyPartComponent? childPart))
                        continue;

                    foreach (var value in GetBodyPartChildren(containedEnt, childPart))
                    {
                        yield return value;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Returns all body part slots for this entity.
    /// </summary>
    public IEnumerable<BodyPartSlot> GetAllBodyPartSlots(
        EntityUid partId,
        BodyPartComponent? part = null)
    {
        if (!Resolve(partId, ref part, logMissing: false))
            yield break;

        foreach (var (slotId, slot) in part.Children)
        {
            yield return slot;

            var containerSlotId = GetOrganContainerId(slotId);

            if (Containers.TryGetContainer(partId, containerSlotId, out var container))
            {
                foreach (var containedEnt in container.ContainedEntities)
                {
                    if (!TryComp(containedEnt, out BodyPartComponent? childPart))
                        continue;

                    foreach (var subSlot in GetAllBodyPartSlots(containedEnt, childPart))
                    {
                        yield return subSlot;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Returns true if the bodyId has any parts of this type.
    /// </summary>
    public bool BodyHasPartType(
        EntityUid bodyId,
        BodyPartType type,
        BodyComponent? body = null)
    {
        return GetBodyChildrenOfType(bodyId, type, body).Any();
    }

    /// <summary>
    /// Returns true if the parentId has the specified childId.
    /// </summary>
    public bool PartHasChild(
        EntityUid parentId,
        EntityUid childId,
        BodyPartComponent? parent,
        BodyPartComponent? child)
    {
        if (!Resolve(parentId, ref parent, logMissing: false)
            || !Resolve(childId, ref child, logMissing: false))
        {
            return false;
        }

        foreach (var (foundId, _) in GetBodyPartChildren(parentId, parent))
        {
            if (foundId == childId)
                return true;
        }
        return false;
    }

    /// <summary>
    /// Returns true if the bodyId has the specified partId.
    /// </summary>
    public bool BodyHasChild(
        EntityUid bodyId,
        EntityUid partId,
        BodyComponent? body = null,
        BodyPartComponent? part = null) =>
        false;

    public IEnumerable<(EntityUid Id, BodyPartComponent Component)> GetBodyChildrenOfType(
        EntityUid bodyId,
        BodyPartType type,
        BodyComponent? body = null,
        BodyPartSymmetry? symmetry = null)
    {
        if (!Resolve(bodyId, ref body, logMissing: false))
            yield break;

        foreach (var target in GetWoundableTargets(bodyId, body))
        {
            if (!TryComp<OrganComponent>(target, out var organ) || organ.Category is not { } category)
                continue;

            if (!SurgeryBodyPartMapping.TryGetBodyPartType(category, out var partType, out var sym))
                continue;

            if (partType != type || (symmetry != null && sym != symmetry))
                continue;

            if (TryComp<BodyPartComponent>(target, out var bodyPart))
                yield return (target, bodyPart);
        }
    }

    /// <summary>
    ///     Returns a list of ValueTuples of <see cref="T"/> and OrganComponent on each organ
    ///     in the given part.
    /// </summary>
    /// <param name="uid">The part entity id to check on.</param>
    /// <param name="part">The part to check for organs on.</param>
    /// <typeparam name="T">The component to check for.</typeparam>
    public List<(EntityUid Owner, T Comp, OrganComponent Organ)> GetBodyPartOrganComponents<T>(
        EntityUid uid,
        BodyPartComponent? part = null)
        where T : IComponent
    {
        if (!Resolve(uid, ref part))
            return new List<(EntityUid owner, T Comp, OrganComponent Organ)>();

        var query = GetEntityQuery<T>();
        var list = new List<(EntityUid Owner, T Comp, OrganComponent Organ)>();

        foreach (var organ in GetPartOrgans(uid, part))
        {
            if (query.TryGetComponent(organ.Id, out var comp))
                list.Add((organ.Id, comp, organ.Component));
        }

        return list;
    }

    /// <summary>
    ///     Tries to get a list of ValueTuples of <see cref="T"/> and OrganComponent on each organ
    ///     in the given part.
    /// </summary>
    /// <param name="uid">The part entity id to check on.</param>
    /// <param name="comps">The list of components.</param>
    /// <param name="part">The part to check for organs on.</param>
    /// <typeparam name="T">The component to check for.</typeparam>
    /// <returns>Whether any were found.</returns>
    public bool TryGetBodyPartOrganComponents<T>(
        EntityUid uid,
        [NotNullWhen(true)] out List<(EntityUid Owner, T Comp, OrganComponent Organ)>? comps,
        BodyPartComponent? part = null)
        where T : IComponent
    {
        if (!Resolve(uid, ref part))
        {
            comps = null;
            return false;
        }

        comps = GetBodyPartOrganComponents<T>(uid, part);

        if (comps.Count != 0)
            return true;

        comps = null;
        return false;
    }

    /// <summary>
    ///     Tries to get a list of ValueTuples of EntityUid and OrganComponent on each organ
    ///     in the given part.
    /// </summary>
    /// <param name="uid">The part entity id to check on.</param>
    /// <param name="type">The type of component to check for.</param>
    /// <param name="organs">Organs found in a body part.</param>
    /// <param name="part">The part to check for organs on.</param>
    /// <returns>Whether any were found.</returns>
    /// <remarks>
    ///     This method is somewhat of a cop out to the fact that we can't use reflection to generically
    ///     get the type of component on runtime due to sandboxing. So we simply do a HasComp check for each organ.
    /// </remarks>
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

    /// <summary>
    /// ЕБАНЫЙ ПИЗДЕЦ
    /// ФУНКЦИЯ ГОВНА!
    /// </summary>
    /// <param name="part"></param>
    /// <returns></returns>
    [Obsolete]
    public string GetSlotFromBodyPart(BodyPartComponent? part)
    {
        var slotName = "";

        if (part is null)
            return slotName;

        slotName = part.SlotId != "" ? part.SlotId : part.PartType.ToString().ToLower();
        return part.Symmetry != BodyPartSymmetry.None ? $"{part.Symmetry.ToString().ToLower()} {slotName}" : slotName;
    }

    // Shitmed Change End

    /// <summary>
    /// Gets the parent body part and all immediate child body parts for the partId.
    /// </summary>
    public IEnumerable<EntityUid> GetBodyPartAdjacentParts(
        EntityUid partId,
        BodyPartComponent? part = null)
    {
        if (!Resolve(partId, ref part, logMissing: false))
            yield break;

        if (TryGetParentBodyPart(partId, out var parentUid, out _))
            yield return parentUid.Value;

        foreach (var containedEnt in part.Children.Keys.Select(slotId => Containers.GetContainer(partId, GetPartSlotContainerId(slotId))).SelectMany(container => container.ContainedEntities))
        {
            yield return containedEnt;
        }
    }

    public IEnumerable<(EntityUid AdjacentId, T Component)> GetBodyPartAdjacentPartsComponents<T>(
        EntityUid partId,
        BodyPartComponent? part = null)
        where T : IComponent
    {
        if (!Resolve(partId, ref part, logMissing: false))
            yield break;

        var query = GetEntityQuery<T>();
        foreach (var adjacentId in GetBodyPartAdjacentParts(partId, part))
        {
            if (query.TryGetComponent(adjacentId, out var component))
                yield return (adjacentId, component);
        }
    }

    public bool TryGetBodyPartAdjacentPartsComponents<T>(
        EntityUid partId,
        [NotNullWhen(true)] out List<(EntityUid AdjacentId, T Component)>? comps,
        BodyPartComponent? part = null)
        where T : IComponent
    {
        if (!Resolve(partId, ref part, logMissing: false))
        {
            comps = null;
            return false;
        }

        var query = GetEntityQuery<T>();
        comps = new List<(EntityUid AdjacentId, T Component)>();

        foreach (var adjacentId in GetBodyPartAdjacentParts(partId, part))
        {
            if (query.TryGetComponent(adjacentId, out var component))
                comps.Add((adjacentId, component));
        }

        if (comps.Count != 0)
            return true;

        comps = null;
        return false;
    }

    #endregion
}
