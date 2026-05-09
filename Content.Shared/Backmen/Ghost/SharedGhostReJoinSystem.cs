using Content.Shared.Backmen.CCVar;
using Robust.Shared.Configuration;
using Robust.Shared.Timing;

namespace Content.Shared.Backmen.Ghost;

public abstract partial class SharedGhostReJoinSystem : EntitySystem
{
    [Dependency] protected IConfigurationManager _configurationManager = default!;
    [Dependency] protected IGameTiming _gameTiming = default!;

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
