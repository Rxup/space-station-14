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

    private EntityUid? _localPilotedMech;

    public override void Initialize()
    {
        base.Initialize();

        _pilotableMechQuery = GetEntityQuery<BkmPilotableMechComponent>();

        SubscribeLocalEvent<BkmPilotableMechComponent, VehicleOperatorSetEvent>(OnOperatorSet);
        SubscribeLocalEvent<VehicleOperatorComponent, ComponentStartup>(OnOperatorStartup);
        SubscribeLocalEvent<VehicleOperatorComponent, AfterAutoHandleStateEvent>(OnOperatorHandleState);
        SubscribeLocalEvent<VehicleOperatorShutdownEvent>(OnOperatorShutdown);
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

    private void UpdateLocalPilotedMech(Entity<VehicleOperatorComponent> ent)
    {
        if (ent.Owner != _playerManager.LocalEntity)
            return;

        if (ent.Comp.Vehicle is { } vehicle && _pilotableMechQuery.HasComp(vehicle))
            SetLocalPilotedMech(vehicle);
        else
            SetLocalPilotedMech(null);
    }

    private void SetLocalPilotedMech(EntityUid? mech)
    {
        if (_localPilotedMech == mech)
            return;

        _localPilotedMech = mech;
        LocalPilotedMechChanged?.Invoke(mech);
    }
}
