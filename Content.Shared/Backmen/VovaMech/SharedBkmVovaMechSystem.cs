using Content.Shared.ActionBlocker;
using Content.Shared.DoAfter;
using Content.Shared.DragDrop;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Vehicle;
using Content.Shared.Vehicle.Components;
using Robust.Shared.Containers;
using Robust.Shared.Serialization;
using System.Diagnostics.CodeAnalysis;

namespace Content.Shared.Backmen.VovaMech;

public abstract partial class SharedBkmVovaMechSystem : EntitySystem
{
    [Dependency] private ActionBlockerSystem _actionBlocker = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] protected VehicleSystem Vehicle = default!;

    private EntityQuery<BkmPilotableMechComponent> _pilotableMechQuery;
    private EntityQuery<VehicleOperatorComponent> _vehicleOperatorQuery;

    public override void Initialize()
    {
        base.Initialize();

        _pilotableMechQuery = GetEntityQuery<BkmPilotableMechComponent>();
        _vehicleOperatorQuery = GetEntityQuery<VehicleOperatorComponent>();

        SubscribeLocalEvent<BkmPilotableMechComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<BkmPilotableMechComponent, DragDropTargetEvent>(OnDragDrop);
        SubscribeLocalEvent<BkmPilotableMechComponent, CanDropTargetEvent>(OnCanDragDrop);

        SubscribeAllEvent<BkmVovaMechSetHandEvent>(OnSetHand);

        // start-backmen: vova-mech-gun-holder
        SubscribeLocalEvent<GetGunHandsHolderEvent>(OnGetGunHandsHolder);
        // end-backmen: vova-mech-gun-holder

        // start-backmen: vova-mech-interaction
        SubscribeLocalEvent<VehicleOperatorComponent, GetUsedEntityEvent>(OnGetUsedEntity);
        SubscribeLocalEvent<VehicleOperatorComponent, AccessibleOverrideEvent>(OnAccessibleOverride);
        SubscribeLocalEvent<VehicleOperatorComponent, InRangeOverrideEvent>(OnInRangeOverride);
        // end-backmen: vova-mech-interaction
    }

    private bool TryGetPilotableMechHandsHolder(EntityUid entity, out EntityUid holder)
    {
        holder = entity;

        if (!_vehicleOperatorQuery.TryComp(entity, out var vehicleOperator) ||
            vehicleOperator.Vehicle is not { } vehicle ||
            !_pilotableMechQuery.HasComp(vehicle) ||
            !HasComp<HandsComponent>(vehicle))
        {
            return false;
        }

        holder = vehicle;
        return true;
    }

    // start-backmen: vova-mech-gun-holder
    private void OnGetGunHandsHolder(ref GetGunHandsHolderEvent args)
    {
        if (TryGetPilotableMechHandsHolder(args.Entity, out var holder))
            args.Holder = holder;
    }
    // end-backmen: vova-mech-gun-holder

    // start-backmen: vova-mech-interaction
    private bool TryGetPilotableMech(Entity<VehicleOperatorComponent> ent, [NotNullWhen(true)] out EntityUid? mech)
    {
        mech = ent.Comp.Vehicle;

        return mech is { } vehicle && _pilotableMechQuery.HasComp(vehicle);
    }

    private bool IsMechAccessibleTarget(EntityUid mech, EntityUid target)
    {
        if (target == mech)
            return true;

        if (!TryComp<HandsComponent>(mech, out var hands))
            return false;

        return _hands.IsHolding((mech, hands), target);
    }

    private void OnGetUsedEntity(Entity<VehicleOperatorComponent> ent, ref GetUsedEntityEvent args)
    {
        if (args.Handled || args.User != ent.Owner || !TryGetPilotableMech(ent, out var mech))
            return;

        if (_hands.TryGetActiveItem(mech.Value, out var held))
            args.Used = held;
    }

    private void OnAccessibleOverride(Entity<VehicleOperatorComponent> ent, ref AccessibleOverrideEvent args)
    {
        if (args.Handled || args.Accessible || args.User != ent.Owner || !TryGetPilotableMech(ent, out var mech))
            return;

        if (!IsMechAccessibleTarget(mech.Value, args.Target))
            return;

        args.Handled = true;
        args.Accessible = true;
    }

    private void OnInRangeOverride(Entity<VehicleOperatorComponent> ent, ref InRangeOverrideEvent args)
    {
        if (args.Handled || args.User != ent.Owner || !TryGetPilotableMech(ent, out var mech))
            return;

        if (!IsMechAccessibleTarget(mech.Value, args.Target))
            return;

        args.Handled = true;
        args.InRange = true;
    }
    // end-backmen: vova-mech-interaction

    private void OnSetHand(BkmVovaMechSetHandEvent msg, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { } user)
            return;

        if (!TryGetPilotableMechHandsHolder(user, out var holder) || !TryComp<HandsComponent>(holder, out var hands))
            return;

        _hands.TrySetActiveHand((holder, hands), msg.HandName);
    }

    protected void EnsureActiveMechHand(EntityUid mech)
    {
        if (!TryComp<HandsComponent>(mech, out var hands) || hands.ActiveHandId != null)
            return;

        if (hands.SortedHands.Count == 0)
            return;

        _hands.SetActiveHand((mech, hands), hands.SortedHands[0]);
    }

    private void OnStartup(EntityUid uid, BkmPilotableMechComponent component, ComponentStartup args)
    {
        component.PilotSlot = _container.EnsureContainer<ContainerSlot>(uid, component.PilotSlotId);
    }

    private void OnDragDrop(EntityUid uid, BkmPilotableMechComponent component, ref DragDropTargetEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        var doAfterEventArgs = new DoAfterArgs(EntityManager, args.Dragged, component.EntryDelay, new BkmVovaMechEntryEvent(), uid, target: uid)
        {
            BreakOnMove = true,
        };

        _doAfter.TryStartDoAfter(doAfterEventArgs);
    }

    private void OnCanDragDrop(EntityUid uid, BkmPilotableMechComponent component, ref CanDropTargetEvent args)
    {
        args.Handled = true;
        args.CanDrop |= CanInsert(uid, args.Dragged, component);
    }

    protected bool CanInsert(EntityUid uid, EntityUid toInsert, BkmPilotableMechComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return false;

        if (!_actionBlocker.CanMove(toInsert))
            return false;

        if (Vehicle.GetOperatorOrNull(uid) == toInsert)
            return false;

        component.PilotSlot ??= _container.EnsureContainer<ContainerSlot>(uid, component.PilotSlotId);

        return _container.CanInsert(toInsert, component.PilotSlot);
    }

    protected bool TryInsert(EntityUid uid, EntityUid toInsert, BkmPilotableMechComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return false;

        if (!CanInsert(uid, toInsert, component))
            return false;

        return _container.Insert(toInsert, component.PilotSlot);
    }

    public bool TryEject(EntityUid uid, BkmPilotableMechComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return false;

        if (!Vehicle.TryGetOperator(uid, out var operatorEnt))
            return false;

        return _container.RemoveEntity(uid, operatorEnt.Value);
    }
}

[Serializable, NetSerializable]
public sealed partial class BkmVovaMechEntryEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed partial class BkmVovaMechExitEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed class BkmVovaMechSetHandEvent : EntityEventArgs
{
    public string HandName { get; }

    public BkmVovaMechSetHandEvent(string handName)
    {
        HandName = handName;
    }
}
