using Content.Shared.Backmen.Teams;
using Content.Shared.Backmen.Teams.Components;
using Content.Shared.StatusIcon;
using Content.Shared.StatusIcon.Components;
using Robust.Shared.Prototypes;

namespace Content.Client.Backmen.Teams;

public sealed class TdmTeamSystem : SharedTdmTeamSystem
{
    [Dependency] private readonly IPrototypeManager _prototype = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TdmMemberComponent, GetStatusIconsEvent>(OnGetTeamIcon);
    }

    protected override void SetTeam(Entity<TdmMemberComponent?> target, StationTeamMarker team)
    {
        // do nothing on client
    }

    private readonly ProtoId<FactionIconPrototype> TeamA = "TeamAFaction";
    private readonly ProtoId<FactionIconPrototype> TeamB = "TeamBFaction";
    private readonly ProtoId<FactionIconPrototype> TeamNoTeam = "Team0Faction";

    private void OnGetTeamIcon(Entity<TdmMemberComponent> ent, ref GetStatusIconsEvent args)
    {
        var status = ent.Comp.Team switch
        {
            StationTeamMarker.TeamA => _prototype.Index<FactionIconPrototype>(TeamA),
            StationTeamMarker.TeamB => _prototype.Index<FactionIconPrototype>(TeamB),
            _ => _prototype.Index<FactionIconPrototype>(TeamNoTeam),
        };
        args.StatusIcons.Add(status);
    }
}
