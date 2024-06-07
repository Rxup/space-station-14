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

    [ValidatePrototypeId<StatusIconPrototype>]
    private const string TeamA = "TeamAFaction";
    [ValidatePrototypeId<StatusIconPrototype>]
    private const string TeamB = "TeamBFaction";
    [ValidatePrototypeId<StatusIconPrototype>]
    private const string TeamNoTeam = "Team0Faction";

    private void OnGetTeamIcon(Entity<SVSTeamMemberComponent> ent, ref GetStatusIconsEvent args)
    {
        var status = ent.Comp.Team switch
        {
            StationTeamMarker.TeamA => _prototype.Index<StatusIconPrototype>(TeamA),
            StationTeamMarker.TeamB => _prototype.Index<StatusIconPrototype>(TeamB),
            _ => _prototype.Index<StatusIconPrototype>(TeamNoTeam),
        };
        args.StatusIcons.Add(status);
    }
}
