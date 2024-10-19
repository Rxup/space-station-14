using Content.Shared.Backmen.Teams;
using Content.Shared.Roles;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.Server.Backmen.ShipVsShip.Components;

[RegisterComponent]
public sealed partial class ShipVsShipGameComponent : Component
{
    public Dictionary<StationTeamMarker, HashSet<NetUserId>> Players = new();
    public Dictionary<StationTeamMarker, EntityUid> Team = new();
    public Dictionary<StationTeamMarker, HashSet<EntityUid>> Objective = new();

    public Dictionary<StationTeamMarker,HashSet<ProtoId<JobPrototype>>> OverflowJobs = new();
    public StationTeamMarker? Winner;
    public EntityUid? WinnerTarget;
}
