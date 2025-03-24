using Content.Shared.Roles;
using Robust.Shared.Prototypes;
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

    [DataField("availableMedJobs", required: false)]
    public Dictionary<ProtoId<JobPrototype>, int[]> SetupMedAvailableJobs = [];

    [DataField("availableHighJobs", required: false)]
    public Dictionary<ProtoId<JobPrototype>, int[]> SetupHighAvailableJobs = [];

    /// <summary>
    /// The schedule of events to occur.
    /// </summary>
    [ViewVariables]
    [DataField("eventSchedule")]
    public List<(TimeSpan timeOffset, CentComEventId eventId)> EventSchedule = new();
}
