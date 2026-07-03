using Content.Shared.Backmen.VovaMech;
using Content.Shared.Vehicle.Components;
using Robust.Client.Player;
using Robust.Shared.Containers;
using Robust.Shared.GameStates;
using Robust.Shared.Player;

namespace Content.Client.Backmen.VovaMech;

/// <inheritdoc />
public sealed partial class BkmVovaMechSystem : SharedBkmVovaMechSystem
{
    [Dependency] private IPlayerManager _playerManager = default!;

    private EntityQuery<BkmPilotableMechComponent> _pilotableMechQuery;

    /// <summary>
    /// Raised when the local player starts or stops piloting a OneStar mech.
    /// </summary>
    public event Action<EntityUid?>? LocalPilotedMechChanged;

    private EntityUid? _localPilotedMech;

    public override void Initialize()
    {
        base.Initialize();

        _pilotableMechQuery = GetEntityQuery<BkmPilotableMechComponent>();

        SubscribeLocalEvent<BkmPilotableMechComponent, VehicleOperatorSetEvent>(OnOperatorSet);
        SubscribeLocalEvent<BkmPilotableMechComponent, EntRemovedFromContainerMessage>(OnPilotRemovedFromContainer); // backmen: vova-mech-hands-ui
        SubscribeLocalEvent<VehicleOperatorComponent, ComponentStartup>(OnOperatorStartup);
        SubscribeLocalEvent<VehicleOperatorComponent, AfterAutoHandleStateEvent>(OnOperatorHandleState);
        SubscribeLocalEvent<VehicleOperatorShutdownEvent>(OnOperatorShutdown);

        // start-backmen: vova-mech-hands-ui
        SubscribeLocalEvent<LocalPlayerAttachedEvent>(OnLocalPlayerAttached);
        SubscribeLocalEvent<LocalPlayerDetachedEvent>(OnLocalPlayerDetached);
        // end-backmen: vova-mech-hands-ui
    }

    private void OnOperatorSet(Entity<BkmPilotableMechComponent> ent, ref VehicleOperatorSetEvent args)
    {
        var local = _playerManager.LocalEntity;
        if (local == null)
            return;

        if (args.NewOperator == local)
            SetLocalPilotedMech(ent.Owner);
        else if (args.OldOperator == local)
            SetLocalPilotedMech(null);
    }

    private void OnOperatorStartup(Entity<VehicleOperatorComponent> ent, ref ComponentStartup args)
    {
        UpdateLocalPilotedMech(ent);
    }

    private void OnOperatorHandleState(Entity<VehicleOperatorComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        UpdateLocalPilotedMech(ent);
    }

    private void OnOperatorShutdown(ref VehicleOperatorShutdownEvent args)
    {
        if (args.Operator != _playerManager.LocalEntity)
            return;

        SetLocalPilotedMech(null);
    }

    // start-backmen: vova-mech-hands-ui
    private void OnLocalPlayerAttached(LocalPlayerAttachedEvent args)
    {
        RefreshLocalPilotedMech();
    }

    private void OnLocalPlayerDetached(LocalPlayerDetachedEvent args)
    {
        if (!WasPilotingPilotableMech(args.Entity))
            return;

        SetLocalPilotedMech(null);
    }

    private void OnPilotRemovedFromContainer(Entity<BkmPilotableMechComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        if (args.Container.ID != ent.Comp.PilotSlotId)
            return;

        if (args.Entity != _playerManager.LocalEntity)
            return;

        SetLocalPilotedMech(null);
    }

    private bool WasPilotingPilotableMech(EntityUid entity)
    {
        return TryComp<VehicleOperatorComponent>(entity, out var vehicleOperator) &&
               vehicleOperator.Vehicle is { } vehicle &&
               _pilotableMechQuery.HasComp(vehicle);
    }

    private void RefreshLocalPilotedMech()
    {
        var local = _playerManager.LocalEntity;
        if (local == null || !WasPilotingPilotableMech(local.Value))
        {
            SetLocalPilotedMech(null);
            return;
        }

        SetLocalPilotedMech(Comp<VehicleOperatorComponent>(local.Value).Vehicle);
    }
    // end-backmen: vova-mech-hands-ui

    private void UpdateLocalPilotedMech(Entity<VehicleOperatorComponent> ent)
    {
        if (ent.Owner != _playerManager.LocalEntity)
            return;

        RefreshLocalPilotedMech();
    }

    private void SetLocalPilotedMech(EntityUid? mech)
    {
        if (_localPilotedMech == mech)
            return;

        _localPilotedMech = mech;
        LocalPilotedMechChanged?.Invoke(mech);
    }

    public void RequestSetMechHand(string handName)
    {
        RaisePredictiveEvent(new BkmVovaMechSetHandEvent(handName));
    }
}
