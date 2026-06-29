using Content.Server._Lavaland.Shuttles.Systems;
using Content.Shared._Lavaland.Procedural.Components;
using Content.Shared._Lavaland.Shuttles.Components;
using Content.Shared._Lavaland.Shuttles.Systems;
using Content.Shared.Shuttles.Components;
using Content.Shared.Whitelist;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Timing;

namespace Content.Server.Backmen.Lavaland.Shuttles;

/// <summary>
/// Refreshes mining shuttle FTL destinations when lavaland is generated after shuttle mapinit.
/// </summary>
public sealed partial class BkmDockingShuttleDestinationSystem : SharedDockingShuttleSystem
{
    [Dependency] private DockingConsoleSystem _console = default!;
    [Dependency] private EntityWhitelistSystem _whitelist = default!;

    public override void Initialize()
    {
        base.Initialize();

        // FTLDestinationComponent is already subscribed by ShuttleConsoleSystem.
        SubscribeLocalEvent<LavalandMapComponent, ComponentStartup>(OnLavalandMapStartup);
        SubscribeLocalEvent<LavalandMapComponent, ComponentShutdown>(OnLavalandMapShutdown);
    }

    private void OnLavalandMapStartup(EntityUid mapUid, LavalandMapComponent comp, ComponentStartup args)
    {
        // LavalandPlanetSystem adds FTLDestinationComponent after map init on the same entity.
        Timer.Spawn(TimeSpan.Zero, () => TryRefreshFromLavalandMap(mapUid));
    }

    private void OnLavalandMapShutdown(EntityUid mapUid, LavalandMapComponent comp, ComponentShutdown args)
    {
        if (!TryComp<MapComponent>(mapUid, out var map))
            return;

        RemoveDestinationFromAllShuttles(map.MapId);
    }

    private void TryRefreshFromLavalandMap(EntityUid mapUid)
    {
        if (!TryComp<FTLDestinationComponent>(mapUid, out var dest)
            || !TryComp<MapComponent>(mapUid, out var map))
        {
            return;
        }

        if (!dest.Enabled)
            return;

        AddDestinationToAllShuttles(mapUid, dest, map.MapId);
    }

    private void AddDestinationToAllShuttles(EntityUid mapUid, FTLDestinationComponent dest, MapId mapId)
    {
        var changed = false;
        var shuttleQuery = EntityQueryEnumerator<DockingShuttleComponent>();
        while (shuttleQuery.MoveNext(out var shuttleUid, out var shuttle))
        {
            if (_whitelist.IsWhitelistFailOrNull(dest.Whitelist, shuttleUid))
                continue;

            changed |= TryAddDestination((shuttleUid, shuttle), mapUid, mapId);
        }

        if (changed)
            RefreshAllConsoles();
    }

    private void RemoveDestinationFromAllShuttles(MapId mapId)
    {
        var changed = false;
        var shuttleQuery = EntityQueryEnumerator<DockingShuttleComponent>();
        while (shuttleQuery.MoveNext(out var shuttleUid, out var shuttle))
        {
            changed |= TryRemoveDestination((shuttleUid, shuttle), mapId);
        }

        if (changed)
            RefreshAllConsoles();
    }

    private bool TryAddDestination(Entity<DockingShuttleComponent> ent, EntityUid mapUid, MapId mapId)
    {
        foreach (var destination in ent.Comp.Destinations)
        {
            if (destination.Map == mapId)
                return false;
        }

        ent.Comp.Destinations.Add(new DockingDestination()
        {
            Name = Name(mapUid),
            Map = mapId
        });
        Dirty(ent);
        return true;
    }

    private bool TryRemoveDestination(Entity<DockingShuttleComponent> ent, MapId mapId)
    {
        var count = ent.Comp.Destinations.Count;
        ent.Comp.Destinations.RemoveAll(destination => destination.Map == mapId);

        if (ent.Comp.Destinations.Count == count)
            return false;

        Dirty(ent);
        return true;
    }

    private void RefreshAllConsoles()
    {
        var consoleQuery = EntityQueryEnumerator<DockingConsoleComponent>();
        while (consoleQuery.MoveNext(out var uid, out var comp))
        {
            if (TerminatingOrDeleted(uid))
                continue;

            _console.UpdateShuttle((uid, comp));
            _console.UpdateUI((uid, comp));
        }
    }
}
