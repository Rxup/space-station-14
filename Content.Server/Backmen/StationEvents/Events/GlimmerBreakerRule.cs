using System.Linq;
using Content.Server.Backmen.StationEvents.Components;
using Content.Server.Emp;
using Content.Server.Power.Components;
using Content.Server.Shuttles.Components;
using Content.Server.Station.Components;
using Content.Server.StationEvents.Events;
using Content.Shared.Backmen.Arrivals;
using Content.Shared.GameTicking.Components;
using Content.Shared.Station.Components;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Backmen.StationEvents.Events;

public sealed partial class GlimmerBreakerRule : StationEventSystem<GlimmerBreakerRuleComponent>
{
    [Dependency] private EmpSystem _emp = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private IGameTiming _timing = default!;

    protected override void Started(EntityUid uid,
        GlimmerBreakerRuleComponent component,
        GameRuleComponent gameRule,
        GameRuleStartedEvent args)
    {
        base.Started(uid, component, gameRule, args);

        var mapIds = StationSystem.GetStations()
            .Where(HasComp<StationEventEligibleComponent>)
            .SelectMany(x => Comp<StationDataComponent>(x).Grids)
            .Select(x=> _transform.GetMapId(x))
            .ToHashSet();

        var query = EntityQueryEnumerator<ApcComponent, TransformComponent>();
        var shuttleQuery = GetEntityQuery<ShuttleComponent>();
        var arrivalsProtected = GetEntityQuery<ArrivalsProtectComponent>();

        List<Entity<ApcComponent>> inShuttle = [];
        List<Entity<ApcComponent>> notInShuttle = [];

        while (query.MoveNext(out var owner, out var apc, out var transform))
        {
            var gridUid = transform.GridUid;
            if (gridUid == null || !mapIds.Contains(transform.MapID))
            {
                continue;
            }

            if (!apc.MainBreakerEnabled)
            {
                continue;
            }

            if (arrivalsProtected.HasComponent(owner))
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

        var count = RobustRandom.Next(component.Min, component.Max);

        for (var i = 0; i < count; i++)
        {
            if(inShuttle.Count == 0 && notInShuttle.Count == 0)
                break;

            var item = RobustRandom.PickAndTake(inShuttle.Count > 0 ? inShuttle : notInShuttle);
            component.AffectedApc.Add(item);
        }
    }

    protected override void ActiveTick(EntityUid uid, GlimmerBreakerRuleComponent component, GameRuleComponent gameRule, float frameTime)
    {
        base.ActiveTick(uid, component, gameRule, frameTime);
        if(component.NextPulse > _timing.CurTime)
            return;

        component.NextPulse =  _timing.CurTime + TimeSpan.FromSeconds(component.DurationSeconds);
        foreach (var apc in component.AffectedApc)
        {
            if(TerminatingOrDeleted(apc) || IsPaused(apc))
                continue;

            _emp.DoEmpEffects(apc, 1000, TimeSpan.FromSeconds(component.DurationSeconds));
        }
    }
}
