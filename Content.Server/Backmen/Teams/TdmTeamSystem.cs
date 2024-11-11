using Content.Shared.Backmen.Teams;
using Content.Shared.Backmen.Teams.Components;

namespace Content.Server.Backmen.Teams;

public sealed class TdmTeamSystem : SharedTdmTeamSystem
{
    protected override void SetTeam(Entity<TdmMemberComponent?> target, StationTeamMarker team)
    {
        SetFaction((target, EnsureComp<TdmMemberComponent>(target)), team);
    }
}
