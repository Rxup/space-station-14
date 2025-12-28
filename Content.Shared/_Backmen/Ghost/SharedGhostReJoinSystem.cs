using Content.Shared._Backmen.CCVar;
using Robust.Shared.Configuration;
using Robust.Shared.Timing;

namespace Content.Shared._Backmen.Ghost;

public abstract class SharedGhostReJoinSystem : EntitySystem
{
    [Dependency] protected readonly IConfigurationManager _configurationManager = default!;
    [Dependency] protected readonly IGameTiming _gameTiming = default!;

    public override void Initialize()
    {
        base.Initialize();

        _configurationManager.OnValueChanged(CCVars.GhostRespawnTime,
            ghostRespawnTime =>
            {
                _ghostRespawnTime = TimeSpan.FromMinutes(ghostRespawnTime);
            },
            true);
    }

    protected TimeSpan _ghostRespawnTime = new(0, 30, 0);
}
