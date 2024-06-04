using Robust.Shared.Map;
using Robust.Shared.Random;
using Content.Server.GameTicking.Rules.Components;
using Content.Server.Power.Components;
using Content.Server.Station.Systems;
using Content.Server.Backmen.Psionics.Glimmer;
using Content.Server.Backmen.StationEvents.Components;
using Content.Server.GameTicking.Components;
using Content.Server.Station.Components;
using Content.Server.StationEvents.Events;
using Content.Shared.Backmen.Psionics.Glimmer;
using Content.Shared.Construction.EntitySystems;
using Robust.Server.GameObjects;
using Robust.Shared.Map.Components;

namespace Content.Server.Backmen.StationEvents.Events;

internal sealed class FreeProberRule : StationEventSystem<FreeProberRuleComponent>
{
    [Dependency] private readonly IRobustRandom _robustRandom = default!;
    [Dependency] private readonly MapSystem _mapSystem = default!;
    [Dependency] private readonly AnchorableSystem _anchorable = default!;
    [Dependency] private readonly GlimmerSystem _glimmerSystem = default!;
    [Dependency] private readonly StationSystem _stationSystem = default!;

    private static readonly string ProberPrototype = "GlimmerProber";
    private static readonly int SpawnDirections = 4;

    protected override void Started(EntityUid uid, FreeProberRuleComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        base.Started(uid, component, gameRule, args);

        List<Entity<TransformComponent>> possibleSpawns = new();

        var query = EntityQueryEnumerator<GlimmerSourceComponent,TransformComponent>();
        while (query.MoveNext(out var glimmerSource, out var glimmerSourceComponent, out var transformComponent))
        {
            if (glimmerSourceComponent is { AddToGlimmer: true, Active: true })
            {
                possibleSpawns.Add((glimmerSource,transformComponent));
            }
        }

        if (possibleSpawns.Count == 0 || _glimmerSystem.Glimmer >= 500 || _robustRandom.Prob(0.25f))
        {
            var queryBattery = EntityQueryEnumerator<PowerNetworkBatteryComponent,TransformComponent>();
            while (queryBattery.MoveNext(out var battery, out var _, out var transformComponent))
            {
                possibleSpawns.Add((battery,transformComponent));
            }
        }

        if (possibleSpawns.Count <= 0)
            return;

        _robustRandom.Shuffle(possibleSpawns);

        foreach (var source in possibleSpawns)
        {
            var xform = source.Comp;

            var station = _stationSystem.GetOwningStation(source, xform);

            if (station == null || !HasComp<StationEventEligibleComponent>(station))
                continue;

            var coordinates = xform.Coordinates;
            if (!TryComp<MapGridComponent>(xform.GridUid, out var grid))
                continue;


            var tileIndices = _mapSystem.TileIndicesFor(xform.GridUid.Value, grid, coordinates);

            for (var i = 0; i < SpawnDirections; i++)
            {
                var direction = (DirectionFlag) (1 << i);
                var offsetIndices = tileIndices.Offset(direction.AsDir());

                // This doesn't check against the prober's mask/layer, because it hasn't spawned yet...
                if (!_anchorable.TileFree(grid, offsetIndices))
                    continue;

                Spawn(ProberPrototype, _mapSystem.GridTileToLocal(xform.GridUid.Value, grid, offsetIndices));
                return;
            }
        }
    }
}
