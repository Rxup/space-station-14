using Content.Shared.Backmen.ShipVsShip;
using Content.Shared.StatusIcon;
using Content.Shared.StatusIcon.Components;
using Robust.Shared.Prototypes;

namespace Content.Client.Backmen.ShipVsShip;

public sealed class TeamMarkerSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototype = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SVSTeamMemberComponent, GetStatusIconsEvent>(OnGetTeamIcon);
    }

    [ValidatePrototypeId<FactionIconPrototype>]
    private const string TeamA = "TeamAFaction";
    [ValidatePrototypeId<FactionIconPrototype>]
    private const string TeamB = "TeamBFaction";
    [ValidatePrototypeId<FactionIconPrototype>]
    private const string TeamNoTeam = "Team0Faction";

    private void OnGetTeamIcon(Entity<SVSTeamMemberComponent> ent, ref GetStatusIconsEvent args)
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
