using Content.Server.Polymorph.Systems;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Backmen.Boss;

public sealed class TimedPolymorphSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly PolymorphSystem _polySystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TimedPolymorphComponent, MapInitEvent>(OnMapInit);
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

            q.Add((uid, timerPoly));

            timerPoly.NextFire += timerPoly.IntervalSeconds;
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
