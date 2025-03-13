using Content.Server.Backmen.Psionics.Glimmer.Components;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Rules;
using Content.Server.StationEvents;
using Content.Shared.Backmen.Psionics.Glimmer;
using Content.Shared.GameTicking.Components;
using Robust.Shared.Random;

namespace Content.Server.Backmen.Psionics.Glimmer;

/// <summary>
/// Система, запускающая события в зависимости от уровня сияния.
/// </summary>
public sealed class GlimmerStationEventSchedulerSystem : GameRuleSystem<GlimmerStationEventSchedulerComponent>
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly GlimmerSystem _glimmerSystem = default!;
    [Dependency] private readonly EventManagerSystem _event = default!;

    protected override void Started(EntityUid uid, GlimmerStationEventSchedulerComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        base.Started(uid, component, gameRule, args);
        PickNextEventTime(uid, component);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!_event.EventsEnabled)
            return;

        var query = EntityQueryEnumerator<GlimmerStationEventSchedulerComponent, GameRuleComponent>();
        while (query.MoveNext(out var uid, out var scheduler, out var gameRule))
        {
            if (!GameTicker.IsGameRuleActive(uid, gameRule))
                continue;

            if (scheduler.TimeUntilNextEvent > 0f)
            {
                scheduler.TimeUntilNextEvent -= frameTime;
                continue;
            }

            PickNextEventTime(uid, scheduler);
            _event.RunRandomEvent(scheduler.ScheduledGameRules);
        }
    }

    /// <summary>
    /// Задаёт время до следующего события в зависимости от уровня сияния.
    /// </summary>
    private void PickNextEventTime(EntityUid uid, GlimmerStationEventSchedulerComponent component)
    {
        var tier = _glimmerSystem.GetGlimmerTier();
        var glimmerMod = GetGlimmerModifier(tier);

        // Интервал задается на основе значений из компонента
        component.TimeUntilNextEvent = _random.NextFloat(
            component.MinEventInterval / glimmerMod,
            component.MaxEventInterval / glimmerMod
        );
    }

    /// <summary>
    /// Модификатор частоты событий в зависимости от уровня сияния.
    /// </summary>
    private float GetGlimmerModifier(GlimmerTier tier)
    {
        return tier switch
        {
            GlimmerTier.Minimal => 1f,
            GlimmerTier.Low => 1.5f,
            GlimmerTier.Moderate => 2f,
            GlimmerTier.High => 3f,
            GlimmerTier.Dangerous => 4f,
            _ => 5f, // Critical
        };
    }
}
