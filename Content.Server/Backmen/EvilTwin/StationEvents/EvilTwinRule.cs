using Content.Server.GameTicking.Rules.Components;
using Content.Server.StationEvents.Events;
using Robust.Shared.Random;

namespace Content.Server.Backmen.EvilTwin.StationEvents;
public sealed class EvilTwinRule : StationEventSystem<EvilTwinRuleComponent>
{
    protected override void Started(EntityUid uid, EvilTwinRuleComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        base.Started(uid, component, gameRule, args);

        if(_evilTwinSystem.MakeTwin(out var _cloneSpawnPoint)){
            cloneSpawnPoint = _cloneSpawnPoint;
        }
        else
        {
            Sawmill.Warning("Map not have latejoin spawnpoints for creating evil twin spawner");
        }
    }

    protected override void Ended(EntityUid uid, EvilTwinRuleComponent component, GameRuleComponent gameRule, GameRuleEndedEvent args)
    {
        base.Ended(uid, component, gameRule, args);
        if (cloneSpawnPoint!=null && cloneSpawnPoint.Value.Valid)
        {
            QueueDel(cloneSpawnPoint.Value);
        }
    }

    private EntityUid? cloneSpawnPoint { get; set;}

    [Dependency] private readonly IRobustRandom _random = default!;

    [Dependency] private readonly EvilTwinSystem _evilTwinSystem = default!;
}
