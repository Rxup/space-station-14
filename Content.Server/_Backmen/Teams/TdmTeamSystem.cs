using Content.Shared._Backmen.Teams;
using Content.Shared._Backmen.Teams.Components;

namespace Content.Server._Backmen.Teams;

public sealed class TdmTeamSystem : SharedTdmTeamSystem
{
    protected override void SetTeam(Entity<TdmMemberComponent?> target, StationTeamMarker team)
    {
        SetFaction((target, EnsureComp<TdmMemberComponent>(target)), team);
    }
}
