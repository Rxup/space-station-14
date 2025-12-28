using Robust.Shared.GameStates;

namespace Content.Shared._Backmen.Teams.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, Access(typeof(SharedTdmTeamSystem))]
public sealed partial class TdmMemberComponent : Component
{
    [AutoNetworkedField]
    public StationTeamMarker Team { get; set; } = StationTeamMarker.Neutral;
}
