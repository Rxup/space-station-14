using Content.Shared.Antag;
using Content.Shared.StatusIcon;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Backmen.ShipVsShip;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class SVSTeamMemberComponent : Component, IAntagStatusIconComponent
{
    [AutoNetworkedField]
    public StationTeamMarker Team { get; set; } = StationTeamMarker.Neutral;
    [AutoNetworkedField]
    public ProtoId<StatusIconPrototype> StatusIcon { get; set; } = "Team0Faction";
    public bool IconVisibleToGhost { get; set; } = true;
}
