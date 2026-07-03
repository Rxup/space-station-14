using Content.Shared.ActionBlocker;
using Content.Shared.DoAfter;
using Content.Shared.DragDrop;
using Content.Shared.Hands.Components;
using Content.Shared.Vehicle;
using Content.Shared.Vehicle.Components;
using Robust.Shared.Containers;
using Robust.Shared.Serialization;

namespace Content.Shared.Backmen.VovaMech;

public abstract partial class SharedBkmVovaMechSystem : EntitySystem
{
    [Dependency] private ActionBlockerSystem _actionBlocker = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
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
    }

    /// <summary>
    /// When piloting a OneStar mech, hand input and weapons resolve to the mech entity.
    /// </summary>
    public EntityUid GetHandsHolder(EntityUid entity)
    {
        if (_vehicleOperatorQuery.TryComp(entity, out var vehicleOperator) &&
            vehicleOperator.Vehicle is { } vehicle &&
            _pilotableMechQuery.HasComp(vehicle) &&
            HasComp<HandsComponent>(vehicle))
        {
            return vehicle;
        }

        return entity;
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
