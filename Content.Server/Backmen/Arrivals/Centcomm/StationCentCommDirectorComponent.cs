using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Server.Backmen.Arrivals.CentComm;

[RegisterComponent]
public sealed partial class StationCentCommDirectorComponent : Component
{
    /// <summary>
    /// Keeps track of the internal event scheduler.
    /// </summary>
    [ViewVariables]
    [DataField("nextEventTick", customTypeSerializer: typeof(TimeOffsetSerializer))]
    public TimeSpan NextEventTick;

    /// <summary>
    /// The schedule of events to occur.
    /// </summary>
    [ViewVariables]
    [DataField("eventSchedule")]
    public List<(TimeSpan timeOffset, CentComEventId eventId)> EventSchedule = new();
}
