/*using Content.Server.Backmen.Shipwrecked.StationEvents.Components;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Rules.Components;
using Content.Server.StationEvents;
using Robust.Shared.Random;

namespace Content.Server.Backmen.Shipwrecked;

public sealed class ShipwreckedEventSchedulerSystem
{

    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly EventManagerSystem _event = default!;

    protected override void Ended(EntityUid uid, ShipwreckedEventSchedulerComponent component, GameRuleComponent gameRule,
    GameRuleEndedEvent args)
    {
        component.TimeUntilNextEvent = ShipwreckedEventSchedulerComponent.MinimumTimeUntilFirstEvent;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!_event.EventsEnabled)
            return;

        var query = EntityQueryEnumerator<ShipwreckedEventSchedulerComponent, GameRuleComponent>();
        while (query.MoveNext(out var uid, out var eventScheduler, out var gameRule))
        {
            if (!GameTicker.IsGameRuleActive(uid, gameRule))
                continue;

            if (eventScheduler.TimeUntilNextEvent > 0)
            {
                eventScheduler.TimeUntilNextEvent -= frameTime;
                return;
            }

            _event.RunRandomEvent();
            ResetTimer(eventScheduler);
        }
    }

    /// <summary>
    /// Reset the event timer once the event is done.
    /// </summary>
    private void ResetTimer(ShipwreckedEventSchedulerComponent component)
    {
        // 10 - 25 minutes. TG does 3-10 but that's pretty frequent
        component.TimeUntilNextEvent = _random.Next(600, 1500);
    }
}*/
