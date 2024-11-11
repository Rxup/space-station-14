using Content.Server.Backmen.StationEvents.Components;
using Content.Server.StationEvents.Events;
using Content.Shared.Backmen.Psionics.Glimmer;
using Content.Shared.GameTicking.Components;

namespace Content.Server.Backmen.StationEvents.Events;

public sealed class GlimmerEventSystem: StationEventSystem<GlimmerEventComponent>
{
    [Dependency] private readonly GlimmerSystem _glimmerSystem = default!;

    protected override void Ended(EntityUid uid, GlimmerEventComponent component, GameRuleComponent gameRule, GameRuleEndedEvent args)
    {
        base.Ended(uid, component, gameRule, args);

        var glimmerBurned = RobustRandom.Next(component.GlimmerBurnLower, component.GlimmerBurnUpper);
        _glimmerSystem.Glimmer -= glimmerBurned;

        var reportEv = new GlimmerEventEndedEvent(component.SophicReport, glimmerBurned);
        RaiseLocalEvent(reportEv);
    }
}


public sealed class GlimmerEventEndedEvent(string message, int glimmerBurned) : EntityEventArgs
{
    public readonly string Message = message;
    public readonly int GlimmerBurned = glimmerBurned;
}
