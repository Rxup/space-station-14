namespace Content.Server.Backmen.Shipwrecked.StationEvents.Components;

public sealed class ShipwreckedEventSchedulerComponent
{
    public const float MinimumTimeUntilFirstEvent = 600;

    /// <summary>
    /// How long until the next check for an event runs
    /// </summary>
    /// Default value is how long until first event is allowed
    [ViewVariables(VVAccess.ReadWrite)]
    public float TimeUntilNextEvent = MinimumTimeUntilFirstEvent;
}
