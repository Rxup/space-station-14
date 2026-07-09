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

    protected TimeSpan _ghostRespawnTime = TimeSpan.FromMinutes(15);

    protected static TimeSpan GetTimeSinceDeath(IGameTiming timing, TimeSpan timeOfDeath)
    {
        return timing.RealTime - timeOfDeath;
    }

    protected static TimeSpan GetRespawnTimeRemaining(TimeSpan respawnTime, TimeSpan timeSinceDeath)
    {
        var remaining = respawnTime - timeSinceDeath;
        return remaining < TimeSpan.Zero ? TimeSpan.Zero : remaining;
    }

    protected static string FormatRespawnTimeRemaining(TimeSpan remaining)
    {
        return $"{(int) remaining.TotalMinutes:00}:{remaining.Seconds:00}";
    }
}
