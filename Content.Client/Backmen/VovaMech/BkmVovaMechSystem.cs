using Content.Shared.Backmen.VovaMech;
using Content.Shared.Vehicle.Components;
using Robust.Client.Player;

namespace Content.Client.Backmen.VovaMech;

/// <inheritdoc />
public sealed partial class BkmVovaMechSystem : SharedBkmVovaMechSystem
{
    [Dependency] private IPlayerManager _playerManager = default!;

    /// <summary>
    /// Raised when the local player starts or stops piloting a OneStar mech.
    /// </summary>
    public event Action<EntityUid?>? LocalPilotedMechChanged;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BkmPilotableMechComponent, VehicleOperatorSetEvent>(OnOperatorSet);
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
}
