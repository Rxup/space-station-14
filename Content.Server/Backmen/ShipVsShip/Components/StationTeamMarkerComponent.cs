using Content.Shared.Backmen.Teams;
using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.Server.Backmen.ShipVsShip.Components;

[RegisterComponent]
public sealed partial class StationTeamMarkerComponent : Component
{
    [DataField("team")]
    public StationTeamMarker Team = StationTeamMarker.Neutral;

    [DataField("goal")]
    public HashSet<EntProtoId> Goal = new();

    [DataField("requireJobs")]
    public HashSet<ProtoId<JobPrototype>> RequireJobs = new();
}
