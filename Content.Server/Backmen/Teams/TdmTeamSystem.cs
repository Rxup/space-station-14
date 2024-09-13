using Content.Shared.Backmen.Teams;
using Content.Shared.Backmen.Teams.Components;

namespace Content.Server.Backmen.Teams;

public sealed class TdmTeamSystem : SharedTdmTeamSystem
{
    [Dependency] private readonly SharedTdmTeamSystem _teamSystem = default!;

    protected override void SetTeam(Entity<TdmMemberComponent?> target, StationTeamMarker team)
    {
        _teamSystem.SetFaction((target, EnsureComp<TdmMemberComponent>(target)), team);
    }
}
