using Robust.Shared.Random;
using Content.Server.GameTicking.Rules.Components;
using Content.Server.Backmen.Psionics.Glimmer;
using Content.Server.Backmen.StationEvents.Components;
using Content.Server.GameTicking.Components;
using Content.Server.StationEvents.Events;

namespace Content.Server.Backmen.StationEvents.Events;

internal sealed class GlimmerRevenantRule : StationEventSystem<GlimmerRevenantRuleComponent>
{
    [Dependency] private readonly IRobustRandom _random = default!;

    protected override void Started(EntityUid uid, GlimmerRevenantRuleComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        base.Started(uid, component, gameRule, args);

        List<EntityUid> glimmerSources = new();

        var query = EntityQueryEnumerator<GlimmerSourceComponent>();
        while (query.MoveNext(out var source, out _))
        {
            glimmerSources.Add(source);
        }

        if (glimmerSources.Count == 0)
            return;

        var coords = Transform(_random.Pick(glimmerSources)).Coordinates;

        Sawmill.Info($"Spawning revenant at {coords}");
        EntityManager.SpawnEntity(component.RevenantPrototype, coords);
    }
}
