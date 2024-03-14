using System.Numerics;
using Content.Server.Shuttles;
using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Events;
using Content.Server.Shuttles.Systems;
using Content.Shared.Administration;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Shuttles.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Console;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;

namespace Content.Server.Backmen.Administration.Commands;

[AnyCommand]
public sealed class BkmFtlCommand : IConsoleCommand
{
    [Dependency] private readonly IEntityManager _entityManager = default!;

    public string Command => "bkm_ftl";
    public string Description => "ftl helper";
    public string Help => "bkm_ftl <desinationUid>";
    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (shell.Player?.AttachedEntity == null)
        {
            shell.WriteLine(Loc.GetString("shell-wrong-invalid-player"));
            return;
        }

        if (!_entityManager.TryGetComponent<MobStateComponent>(shell.Player.AttachedEntity.Value,
                out var mobStateComponent) || mobStateComponent.CurrentState != MobState.Alive)
        {
            shell.WriteLine(Loc.GetString("shell-wrong-not-alive"));
            return;
        }

        if(args.Length != 1)
        {
            shell.WriteLine(Loc.GetString("shell-wrong-arguments-number"));
            return;
        }

        if (!int.TryParse(args[0], out var ftlInt))
        {
            shell.WriteLine(Loc.GetString("shell-entity-uid-must-be-number"));
            return;
        }

        var gridNet = new NetEntity(ftlInt);

        if (!_entityManager.TryGetEntity(gridNet, out var gridUid))
        {
            shell.WriteLine(Loc.GetString("shell-invalid-entity-id"));
            return;
        }

        var ftl = _entityManager.GetComponentOrNull<FTLDestinationComponent>(gridUid);

        if (ftl == null || !ftl.Enabled || ftl.Whitelist != null || !ftl.BeaconsOnly)
        {
            shell.WriteLine(Loc.GetString("shell-invalid-ftl-destination"));
            return;
        }
        var ftlTransform = _entityManager.GetComponent<TransformComponent>(gridUid.Value);

        var performer = shell.Player.AttachedEntity.Value;

        if (!_entityManager.TryGetComponent<PilotComponent>(performer, out var pilotComponent) || pilotComponent.Console == null)
        {
            shell.WriteLine(Loc.GetString("shell-invalid-no-pilot"));
            return;
        }

        var grid = _entityManager.GetComponent<TransformComponent>(performer);
        if (grid.GridUid == null || ftlTransform.MapUid == null)
        {
            shell.WriteLine(Loc.GetString("shell-invalid-grid"));
            return;
        }

        Entity<TransformComponent> shuttle;

        if (_entityManager.TryGetComponent<DroneConsoleComponent>(pilotComponent.Console, out var droneConsoleComponent) &&
            droneConsoleComponent.Entity != null)
        {
            shell.WriteLine(Loc.GetString("shell-cant-remote-pilot"));
            return;
            //shuttle = (droneConsoleComponent.Entity.Value,_entityManager.GetComponent<TransformComponent>(droneConsoleComponent.Entity.Value));
        }
        else
        {
            shuttle = (grid.GridUid.Value,_entityManager.GetComponent<TransformComponent>(grid.GridUid.Value));
        }

        if (shuttle.Comp.GridUid == null)
        {
            shell.WriteLine(Loc.GetString("shell-invalid-shuttle-grid"));
            return;
        }

        var shuttleSystem = _entityManager.System<ShuttleSystem>();

        if (!shuttleSystem.CanFTL(shuttle.Comp.GridUid.Value, out var reason))
        {
            shell.WriteLine(reason);
            return;
        }

        var transformSystem = _entityManager.System<TransformSystem>();

        var targetMap = transformSystem.GetMapCoordinates(gridUid.Value,ftlTransform);

        // Check shuttle can FTL to this target.
        if (!shuttleSystem.CanFTLTo(shuttle, targetMap.MapId))
        {
            shell.WriteLine(Loc.GetString("shell-invalid-mapid"));
            return;
        }

        if (!_entityManager.TryGetComponent(shuttle, out PhysicsComponent? shuttlePhysics))
        {
            return;
        }

        var targetCoordinates = new EntityCoordinates(ftlTransform.MapUid.Value, transformSystem.GetWorldPosition(ftlTransform));
        var targetAngle = Angle.Zero;

        // Client sends the "adjusted" coordinates and we adjust it back to get the actual transform coordinates.
        var adjustedCoordinates = targetCoordinates.Offset(targetAngle.RotateVec(-shuttlePhysics.LocalCenter));

        var tagEv = new FTLTagEvent();
        _entityManager.EventBus.RaiseLocalEvent(shuttle, ref tagEv);

        var ev = new ShuttleConsoleFTLTravelStartEvent(pilotComponent.Console.Value);
        _entityManager.EventBus.RaiseEvent(EventSource.Local, ref ev);

        shuttleSystem.FTLToCoordinates(shuttle,
            _entityManager.GetComponent<ShuttleComponent>(shuttle),
            adjustedCoordinates,
            targetAngle);

        shell.WriteLine("OK!");
    }

    public CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        return CompletionResult.FromHintOptions(
            CompletionHelper.Components<FTLDestinationComponent>(args[0], _entityManager),
            "Точка назначения");
    }
}
