using System.Linq;
using Content.Server.Backmen.StationEvents.Components;
using Content.Server.Emp;
using Content.Server.Power.Components;
using Content.Server.Shuttles.Components;
using Content.Server.Station.Components;
using Content.Server.Station.Systems;
using Content.Server.StationEvents.Events;
using Content.Shared.GameTicking.Components;
using Robust.Shared.Random;

namespace Content.Server.Backmen.StationEvents.Events;

public sealed class GlimmerBreakerRule : StationEventSystem<GlimmerBreakerRuleComponent>
{
    [Dependency] private readonly IRobustRandom _robustRandom = default!;
    [Dependency] private readonly StationSystem _stationSystem = default!;
    [Dependency] private readonly EmpSystem _emp = default!;

    protected override void Started(EntityUid uid,
        GlimmerBreakerRuleComponent component,
        GameRuleComponent gameRule,
        GameRuleStartedEvent args)
    {
        base.Started(uid, component, gameRule, args);

        var grids = _stationSystem.GetStations()
            .Where(HasComp<StationEventEligibleComponent>)
            .SelectMany(x => Comp<StationDataComponent>(x).Grids)
            .ToHashSet();

        var query = EntityQueryEnumerator<ApcComponent, TransformComponent>();
        var shuttleQuery = GetEntityQuery<ShuttleComponent>();

        List<Entity<ApcComponent>> inShuttle = [];
        List<Entity<ApcComponent>> notInShuttle = [];

        while (query.MoveNext(out var owner, out var apc, out var transform))
        {
            var gridUid = transform.GridUid;
            if (gridUid == null || !grids.Contains(gridUid.Value))
            {
                continue;
            }

            if (!apc.MainBreakerEnabled)
            {
                continue;
            }

            if (shuttleQuery.HasComp(gridUid.Value))
            {
                inShuttle.Add((owner, apc));
            }
            else
            {
                notInShuttle.Add((owner, apc));
            }
        }

        if (inShuttle.Count == 0 && notInShuttle.Count == 0)
        {
            Log.Warning("Apc is not found, nothing to do");
            return;
        }

        var item = _robustRandom.Pick(inShuttle.Count > 0 ? inShuttle : notInShuttle);
        _emp.DoEmpEffects(item, 1000, 10);
    }
}
