using Content.Server.Sandbox;
using Robust.Server.Player;
using Robust.Shared.Map;
using Robust.Shared.Network.Messages;
using System.Text.Json;
using System.Text.Json.Serialization;
using Robust.Shared.Enums;

namespace Content.Server.Backmen.Sandbox;

public sealed class AdminSpawnHandler : EntitySystem
{
    private ISawmill _log = default!;

    public override void Initialize()
    {
        base.Initialize();
        _log = Logger.GetSawmill($"ecs.systems.{nameof(AdminSpawnHandler)}");
        _sandboxSystem.OnAdminPlacement += SandboxSystemOnOnAdminPlacement;
    }

    private void SandboxSystemOnOnAdminPlacement(object? sender, MsgPlacement placement)
    {
        var channel = placement.MsgChannel;
        var session = _playerManager.GetSessionByChannel(channel);

        var user = session.AttachedEntity ?? EntityUid.Invalid;
        EntityCoordinates userPos;
        try
        {
            userPos = Transform(user).Coordinates;
        }
        catch (KeyNotFoundException)
        {
            userPos = EntityCoordinates.Invalid;
        }

        switch (placement.PlaceType)
        {
            case Robust.Shared.Enums.PlacementManagerMessage.RequestPlacement:
                if (placement.IsTile)
                {
                    var tile = IoCManager.Resolve<ITileDefinitionManager>()[(int) placement.TileType].Name;
                    _log.Info(
                        $"Admin {ToPrettyString(session.AttachedEntity ?? EntityUid.Invalid):user} ({userPos:targetlocation}) tiled {tile} ({placement.TileType}) at {placement.EntityCoordinates:targetlocation}");
                }
                else
                {
                    _log.Info(
                        $"Admin {ToPrettyString(session.AttachedEntity ?? EntityUid.Invalid):user} ({userPos:targetlocation}) spawned {placement.EntityTemplateName} at {placement.EntityCoordinates:targetlocation}");
                }

                break;
            case Robust.Shared.Enums.PlacementManagerMessage.RequestEntRemove:
            {
                string ent;
                try
                {
                    ent = MetaData(placement.EntityUid).EntityPrototype?.ID ?? "Invalid";
                }
                catch (KeyNotFoundException)
                {
                    ent = "Invalid";
                }

                EntityCoordinates coords;
                try
                {
                    coords = Transform(user).Coordinates;
                }
                catch (KeyNotFoundException)
                {
                    coords = EntityCoordinates.Invalid;
                }

                _log.Info(
                    $"Admin {ToPrettyString(session.AttachedEntity ?? EntityUid.Invalid):user} ({Transform(user).Coordinates:targetlocation}) delete {ent} at {coords:targetlocation}");
            }
                break;
            case PlacementManagerMessage.StartPlacement:
            case PlacementManagerMessage.CancelPlacement:
            case PlacementManagerMessage.PlacementFailed:
            case PlacementManagerMessage.RequestRectRemove:
            default:
            {
                string ent;
                try
                {
                    ent = MetaData(placement.EntityUid).EntityPrototype?.ID ?? "Invalid";
                }
                catch (KeyNotFoundException)
                {
                    ent = "Invalid";
                }

                EntityCoordinates coords;
                try
                {
                    coords = Transform(user).Coordinates;
                }
                catch (KeyNotFoundException)
                {
                    coords = EntityCoordinates.Invalid;
                }

                _log.Info(
                    $"Admin {ToPrettyString(session.AttachedEntity ?? EntityUid.Invalid):user} ({Transform(user).Coordinates:targetlocation}) {placement.PlaceType} {ent} {coords:targetlocation} {placement.EntityCoordinates:targetlocation} {JsonSerializer.Serialize(new { placement.Align, placement.AlignOption, placement.DirRcv, placement.EntityCoordinates, placement.EntityTemplateName, placement.EntityUid, placement.IsTile })}");
            }

                break;
        }
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _sandboxSystem.OnAdminPlacement -= SandboxSystemOnOnAdminPlacement;
    }

    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly SandboxSystem _sandboxSystem = default!;
}
