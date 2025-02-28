using Robust.Shared.Random;
using Content.Server.GameTicking.Rules.Components;
using Content.Server.Backmen.Psionics.Glimmer;
using Content.Server.Backmen.StationEvents.Components;
using Content.Server.Station.Systems;
using Content.Server.StationEvents.Events;
using Content.Shared.GameTicking.Components;

namespace Content.Server.Backmen.StationEvents.Events;

internal sealed class GlimmerRevenantRule : StationEventSystem<GlimmerRevenantRuleComponent>
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly StationSystem _stationSystem = default!;

    protected override void Started(EntityUid uid, GlimmerRevenantRuleComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        base.Started(uid, component, gameRule, args);

        List<EntityUid> glimmerSources = new();

        if (!TryGetRandomStation(out var station))
        {
            return;
        }

        var query = EntityQueryEnumerator<GlimmerSourceComponent,TransformComponent>();
        while (query.MoveNext(out var source, out _, out var transform))
        {
            if(Paused(source))
                continue;

            if (_stationSystem.GetOwningStation(source, transform) == station)
                glimmerSources.Add(source);
        }

        if (glimmerSources.Count == 0)
            return;

        var coords = Transform(_random.Pick(glimmerSources)).Coordinates;

        Sawmill.Info($"Spawning revenant at {coords}");
        EntityManager.SpawnEntity(component.RevenantPrototype, coords);
    }
}
