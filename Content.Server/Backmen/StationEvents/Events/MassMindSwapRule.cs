using System.Linq;
using Content.Server.Backmen.Abilities.Psionics;
using Content.Server.Backmen.Psionics;
using Content.Server.Backmen.StationEvents.Components;
using Content.Server.GameTicking.Rules.Components;
using Content.Server.Station.Components;
using Content.Server.StationEvents.Events;
using Content.Shared.Backmen.Abilities.Psionics;
using Content.Shared.Mind;
using Content.Shared.Mindshield.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.NPC;
using Content.Shared.Roles;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Random;

namespace Content.Server.Backmen.StationEvents.Events;

/// <summary>
/// Forces a mind swap on all non-insulated potential psionic entities.
/// </summary>
internal sealed class MassMindSwapRule : StationEventSystem<MassMindSwapRuleComponent>
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly MobStateSystem _mobStateSystem = default!;
    [Dependency] private readonly MindSwapPowerSystem _mindSwap = default!;
    [Dependency] private readonly SharedMindSystem _mindSystem = default!;
    [Dependency] private readonly SharedRoleSystem _roleSystem = default!;

    private HashSet<MapId> GetStationEventMaps()
    {
        var stations = new HashSet<MapId>();
        var query = EntityQueryEnumerator<StationDataComponent, StationEventEligibleComponent>();
        while (query.MoveNext(out var data, out _))
        {
            foreach (var gridUid in data.Grids)
            {
                stations.Add(Transform(gridUid).MapID);
            }
        }

        return stations;
    }

    protected override void Started(EntityUid uid, MassMindSwapRuleComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        base.Started(uid, component, gameRule, args);

        List<EntityUid> psionicPool = new();


        var mindswaped = GetEntityQuery<MindSwappedComponent>();
        {
            var stationEvents = GetStationEventMaps();

            var mindshild = GetEntityQuery<MindShieldComponent>();
            var psiIsulated = GetEntityQuery<PsionicInsulationComponent>();

            var query = EntityQueryEnumerator<PotentialPsionicComponent, TransformComponent, MobStateComponent>();
            while (query.MoveNext(out var psion, out _, out var xform, out _))
            {
                if (mindswaped.HasComponent(psion) || mindshild.HasComponent(psion) || psiIsulated.HasComponent(psion))
                {
                    continue;
                }

                if (!_mobStateSystem.IsAlive(psion))
                    continue;

                if (!stationEvents.Contains(xform.MapID))
                    continue;

                if (_mindSystem.TryGetMind(psion, out var mindId, out var mind) && _roleSystem.MindIsExclusiveAntagonist(mindId))
                    continue;

                psionicPool.Add(psion);
            }
        }

        // Shuffle the list of candidates.
        _random.Shuffle(psionicPool);

        var activeNpc = GetEntityQuery<ActiveNPCComponent>();
        var actorQuery = GetEntityQuery<ActorComponent>();

        var q1 = new Queue<EntityUid>(psionicPool.ToList());

        while (q1.TryDequeue(out var actor))
        {
            if (mindswaped.HasComponent(actor)) // skip if swapped
            {
                continue;
            }
            var q2 = new Queue<EntityUid>(psionicPool.ToList());
            while (q2.TryDequeue(out var other))
            {
                if(actor == other)
                    continue;

                if (mindswaped.HasComponent(other)) // skip if swapped
                {
                    continue;
                }
                if(!(actorQuery.HasComponent(actor) && actorQuery.HasComponent(other)))
                {
                    var gridA = Transform(actor).GridUid;
                    var gridB = Transform(other).GridUid;
                    if(gridA == null || gridB == null)
                        continue;
                    if(gridA != gridB)
                        continue;
                }
                if (!_mindSwap.Swap(actor, other))
                {
                    continue;
                }

                if (!actorQuery.HasComponent(actor) || !actorQuery.HasComponent(other))
                {
                    component.IsTemporary = true;
                }
                if (!component.IsTemporary)
                {
                    _mindSwap.GetTrapped(actor);
                    _mindSwap.GetTrapped(other);
                }
                psionicPool.Remove(actor);
                psionicPool.Remove(other);
                break;
            }
        }
    }
}
