using Content.Server.NPC.HTN;
using Content.Server.NPC.HTN.PrimitiveTasks.Operators;
using Content.Server.Polymorph.Systems;
using Content.Shared.CombatMode;
using Content.Shared.NPC;
using Robust.Shared.Player;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Backmen.Boss;

public sealed class TimedPolymorphSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly PolymorphSystem _polySystem = default!;
    private EntityQuery<HTNComponent> _htnQuery;
    private EntityQuery<ActiveNPCComponent> _activeNpcQuery;
    private EntityQuery<CombatModeComponent> _combatModeQuery;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TimedPolymorphComponent, MapInitEvent>(OnMapInit);
        _htnQuery = GetEntityQuery<HTNComponent>();
        _activeNpcQuery = GetEntityQuery<ActiveNPCComponent>();
        _combatModeQuery =  GetEntityQuery<CombatModeComponent>();
    }

    private void OnMapInit(Entity<TimedPolymorphComponent> ent, ref MapInitEvent args)
    {
        ent.Comp.NextFire = _timing.CurTime + ent.Comp.IntervalSeconds;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var curTime = _timing.CurTime;

        var q = new HashSet<Entity<TimedPolymorphComponent>>();

        var query = EntityQueryEnumerator<TimedPolymorphComponent>();
        while (query.MoveNext(out var uid, out var timerPoly))
        {
            if (timerPoly.NextFire > curTime)
                continue;

            timerPoly.NextFire += timerPoly.IntervalSeconds;


            if (timerPoly.InCombatOnly)
            {
                if (_activeNpcQuery.HasComp(uid) && _htnQuery.TryComp(uid, out var htn))
                {
                    if (htn.Plan == null || htn.Plan.CurrentOperator is WaitOperator or MoveToOperator)
                    {
                        continue;
                    }
                }
                else if (_combatModeQuery.TryComp(uid, out var combatMode) && !combatMode.IsInCombatMode)
                {
                    continue;
                }
            }

            q.Add((uid, timerPoly));
        }

        foreach (var ent in q)
        {
            OnTimerFired(ent);
        }
    }

    private void OnTimerFired(Entity<TimedPolymorphComponent> ent)
    {
        if (!_random.Prob(ent.Comp.Chance))
            return;

        var polyProto = _random.Pick(ent.Comp.Prototypes);
        _polySystem.PolymorphEntity(ent, polyProto);
    }
}
