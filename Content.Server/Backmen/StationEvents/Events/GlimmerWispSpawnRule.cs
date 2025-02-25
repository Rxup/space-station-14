using System.Linq;
using Robust.Shared.Random;
using Content.Server.GameTicking.Rules.Components;
using Content.Server.NPC.Components;
using Content.Server.Backmen.Psionics.Glimmer;
using Content.Server.Backmen.StationEvents.Components;
using Content.Server.Station.Systems;
using Content.Server.StationEvents.Components;
using Content.Server.StationEvents.Events;
using Content.Shared.Backmen.Psionics.Glimmer;
using Content.Shared.Backmen.Abilities.Psionics;
using Content.Shared.GameTicking.Components;
using Content.Shared.NPC.Components;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server.Backmen.StationEvents.Events;

internal sealed class GlimmerWispRule : StationEventSystem<GlimmerWispRuleComponent>
{
    [Dependency] private readonly IRobustRandom _robustRandom = default!;
    [Dependency] private readonly GlimmerSystem _glimmerSystem = default!;
    [Dependency] private readonly StationSystem _stationSystem = default!;

    [ValidatePrototypeId<EntityPrototype>] private static readonly string WispPrototype = "MobGlimmerWisp";

    protected override void Started(EntityUid uid, GlimmerWispRuleComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        base.Started(uid, component, gameRule, args);

        if (!TryGetRandomStation(out var station))
        {
            return;
        }

        var glimmerSources = new List<EntityCoordinates>();
        {
            var locations = EntityQueryEnumerator<GlimmerSourceComponent, TransformComponent>();
            while (locations.MoveNext(out var sUid, out _, out var transform))
            {
                if(Paused(sUid))
                    continue;

                if (_stationSystem.GetOwningStation(sUid, transform) == station)
                {
                    glimmerSources.Add(transform.Coordinates);
                }
            }
        }

        var hiddenSpawnLocations = new List<EntityCoordinates>();
        {
            var locations = EntityQueryEnumerator<VentCritterSpawnLocationComponent, TransformComponent>();
            while (locations.MoveNext(out var sUid, out _, out var transform))
            {
                if (_stationSystem.GetOwningStation(sUid, transform) == station)
                {
                    hiddenSpawnLocations.Add(transform.Coordinates);
                }
            }
        }

        var baseCount = Math.Max(1, EntityManager.EntityQuery<PsionicComponent, NpcFactionMemberComponent>().Count() / 10);
        var multiplier = Math.Max(1, (int) _glimmerSystem.GetGlimmerTier() - 2);

        var total = baseCount * multiplier;

        for (var i = 0; i < total; i++)
        {
            if (glimmerSources.Count != 0 && _robustRandom.Prob(0.4f))
            {
                Spawn(WispPrototype, _robustRandom.Pick(glimmerSources));
                i++;
                continue;
            }

            if (hiddenSpawnLocations.Count != 0)
            {
                Spawn(WispPrototype, _robustRandom.Pick(hiddenSpawnLocations));
                i++;
                continue;
            }
            return;
        }
    }
}
