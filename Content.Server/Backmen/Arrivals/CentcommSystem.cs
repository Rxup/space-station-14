using System.Numerics;
using Content.Server.GameTicking;
using Content.Server.Popups;
using Content.Server.Salvage.Expeditions;
using Content.Server.Shuttle.Components;
using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Systems;
using Content.Server.Station.Components;
using Content.Server.Station.Systems;
using Content.Shared.Cargo.Components;
using Content.Shared.Shuttles.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Utility;

namespace Content.Server.Backmen.Arrivals;

public sealed class CentcommSystem : EntitySystem
{
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly ShuttleSystem _shuttleSystem = default!;
    [Dependency] private readonly StationSystem _stationSystem = default!;
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ActorComponent, CentcomFtlAction>(OnFtlActionUsed);
        SubscribeLocalEvent<PreGameMapLoad>(OnPreGameMapLoad, after: new[]{typeof(StationSystem)});
    }

    private void OnPreGameMapLoad(PreGameMapLoad ev)
    {
        if (ev.GameMap.ID != "CentComm")
        {
            return;
        }

        ev.Options.Offset = new Vector2(0, 0);
    }

    private void OnFtlActionUsed(EntityUid uid, ActorComponent component, CentcomFtlAction args)
    {
        var grid = Transform(args.Performer);
        if (grid.GridUid == null)
        {
            return;
        }

        if (!TryComp<PilotComponent>(args.Performer, out var pilotComponent) || pilotComponent.Console == null)
        {
            _popup.PopupEntity(Loc.GetString("centcom-ftl-action-no-pilot"), args.Performer, args.Performer);
            return;
        }

        TransformComponent shuttle;

        if (TryComp<DroneConsoleComponent>(pilotComponent.Console, out var droneConsoleComponent) && droneConsoleComponent.Entity != null)
        {
            shuttle = Transform(droneConsoleComponent.Entity.Value);
        }
        else
        {
            shuttle = grid;
        }


        if (!TryComp<ShuttleComponent>(shuttle.GridUid, out var comp) || HasComp<FTLComponent>(shuttle.GridUid) || (
                HasComp<BecomesStationComponent>(shuttle.GridUid) &&
                    !(
                    HasComp<SalvageShuttleComponent>(shuttle.GridUid) ||
                    HasComp<CargoShuttleComponent>(shuttle.GridUid)
                    )
                ))
        {
            return;
        }

        var stationUid = _stationSystem.GetStations().FirstOrNull(HasComp<StationCentcommComponent>);

        if (!TryComp<StationCentcommComponent>(stationUid, out var centcomm) ||
            Deleted(centcomm.Entity))
        {
            _popup.PopupEntity(Loc.GetString("centcom-ftl-action-no-station"), args.Performer, args.Performer);
            return;
        }

        if (shuttle.MapID == centcomm.MapId)
        {
            _popup.PopupEntity(Loc.GetString("centcom-ftl-action-at-centcomm"), args.Performer, args.Performer);
            return;
        }

        if (!_shuttleSystem.CanFTL(shuttle.GridUid, out var reason))
        {
            _popup.PopupEntity(reason, args.Performer, args.Performer);
            return;
        }

        _shuttleSystem.FTLTravel(shuttle.GridUid.Value, comp, centcomm.Entity);
    }
}
