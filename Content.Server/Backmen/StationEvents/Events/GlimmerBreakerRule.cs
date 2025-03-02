using System.Linq;
using Content.Server.Backmen.StationEvents.Components;
using Content.Server.Power.Components;
using Content.Server.Shuttles.Components;
using Content.Server.Station.Components;
using Content.Server.Station.Systems;
using Content.Server.StationEvents.Events;
using Content.Shared.Backmen.Psionics.Glimmer;
using Content.Shared.Construction.EntitySystems;
using Content.Shared.GameTicking.Components;
using Content.Shared.Station.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Random;

namespace Content.Server.Backmen.StationEvents.Events;

public sealed class GlimmerBreakerRule : StationEventSystem<GlimmerBreakerRuleComponent>
{
    [Dependency] private readonly IRobustRandom _robustRandom = default!;
    [Dependency] private readonly MapSystem _mapSystem = default!;
    [Dependency] private readonly AnchorableSystem _anchorable = default!;
    [Dependency] private readonly GlimmerSystem _glimmerSystem = default!;
    [Dependency] private readonly StationSystem _stationSystem = default!;

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
        List<Entity<ApcComponent>> inShuttle = [];
        List<Entity<ApcComponent>> notInShuttle = [];

        while (query.MoveNext(out var owner, out var apc, out var transform))
        {
            var gridUid = transform.GridUid;
            if (gridUid == null || !grids.Contains(gridUid.Value))
            {
                continue;
            }

            var isShuttle = HasComp<ShuttleComponent>(gridUid.Value);
            if (!apc.MainBreakerEnabled)
            {
                continue;
            }

            if (isShuttle)
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
        item.Comp.MainBreakerEnabled = false;
        item.Comp.NeedStateUpdate = true;
        return;
    }
}
