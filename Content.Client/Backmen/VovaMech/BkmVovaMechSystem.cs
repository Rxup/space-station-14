using Content.Shared.Backmen.VovaMech;
using Content.Shared.Vehicle.Components;
using Robust.Client.Player;
using Robust.Shared.GameStates;

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

    public override void Initialize()
    {
        base.Initialize();

        _pilotableMechQuery = GetEntityQuery<BkmPilotableMechComponent>();

        SubscribeLocalEvent<BkmPilotableMechComponent, VehicleOperatorSetEvent>(OnOperatorSet);
        SubscribeLocalEvent<VehicleOperatorComponent, ComponentStartup>(OnOperatorStartup);
        SubscribeLocalEvent<VehicleOperatorComponent, AfterAutoHandleStateEvent>(OnOperatorHandleState);
        SubscribeLocalEvent<VehicleOperatorComponent, ComponentShutdown>(OnOperatorShutdown);
    }

    private void OnOperatorSet(Entity<BkmPilotableMechComponent> ent, ref VehicleOperatorSetEvent args)
    {
        var local = _playerManager.LocalEntity;
        if (local == null)
            return;

        if (args.NewOperator == local)
            LocalPilotedMechChanged?.Invoke(ent.Owner);
        else if (args.OldOperator == local)
            LocalPilotedMechChanged?.Invoke(null);
    }

    private void OnOperatorStartup(Entity<VehicleOperatorComponent> ent, ref ComponentStartup args)
    {
        UpdateLocalPilotedMech(ent);
    }

    private void OnOperatorHandleState(Entity<VehicleOperatorComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        UpdateLocalPilotedMech(ent);
    }

    private void OnOperatorShutdown(Entity<VehicleOperatorComponent> ent, ref ComponentShutdown args)
    {
        if (ent.Owner != _playerManager.LocalEntity)
            return;

        LocalPilotedMechChanged?.Invoke(null);
    }

    private void UpdateLocalPilotedMech(Entity<VehicleOperatorComponent> ent)
    {
        if (ent.Owner != _playerManager.LocalEntity)
            return;

        if (ent.Comp.Vehicle is { } vehicle && _pilotableMechQuery.HasComp(vehicle))
            LocalPilotedMechChanged?.Invoke(vehicle);
        else
            LocalPilotedMechChanged?.Invoke(null);
    }
}
