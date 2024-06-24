using Content.Server.Backmen.Fugitive;
using Content.Server.Backmen.StationEvents.Components;
using Content.Server.StationEvents.Events;
using Content.Shared.GameTicking.Components;

namespace Content.Server.Backmen.StationEvents.Events;

public sealed class FugitiveRule : StationEventSystem<FugitiveRuleComponent>
{
    protected override void Started(EntityUid uid, FugitiveRuleComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        base.Started(uid, component, gameRule, args);

        if(!_fugitiveSystem.MakeFugitive(out _))
        {
            Sawmill.Warning("Map not have latejoin spawnpoints for creating fugitive spawner");
        }
    }

    [Dependency] private readonly FugitiveSystem _fugitiveSystem = default!;
}
