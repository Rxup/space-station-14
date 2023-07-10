using Content.Server.Popups;
using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Systems;
using Content.Server.Station.Systems;
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
    }

    private void OnFtlActionUsed(EntityUid uid, ActorComponent component, CentcomFtlAction args)
    {
        var grid = Transform(args.Performer);
        if (grid.GridUid == null)
        {
            return;
        }

        if (!TryComp<ShuttleComponent>(grid.GridUid, out var comp))
        {
            return;
        }

        if (!TryComp<PilotComponent>(args.Performer, out var pilotComponent))
        {
            _popup.PopupEntity(Loc.GetString("centcom-ftl-action-no-pilot"), args.Performer, args.Performer);
            return;
        }


        var stationUid = _stationSystem.GetStations().FirstOrNull();

        if (!TryComp<StationCentcommComponent>(stationUid, out var centcomm) ||
            Deleted(centcomm.Entity))
        {
            _popup.PopupEntity(Loc.GetString("centcom-ftl-action-no-station"), args.Performer, args.Performer);
            return;
        }

        if (grid.MapID == centcomm.MapId)
        {
            _popup.PopupEntity(Loc.GetString("centcom-ftl-action-at-centcomm"), args.Performer, args.Performer);
            return;
        }

        if (!_shuttleSystem.CanFTL(grid.GridUid, out var reason))
        {
            _popup.PopupEntity(reason, args.Performer, args.Performer);
            return;
        }

        _shuttleSystem.FTLTravel(grid.GridUid.Value, comp, centcomm.Entity);
    }
}
