using Robust.Shared.GameStates;

namespace Content.Shared.Backmen.Ghost.Roles.Components;

[RegisterComponent]
[NetworkedComponent]
[AutoGenerateComponentState(true)]
public sealed partial class GhostVisRollerComponent : Component
{
    [DataField, AutoNetworkedField]
    public uint CurrentId = 0;
    [DataField, AutoNetworkedField]
    public Dictionary<string,float> Bids = new();
    [DataField, AutoNetworkedField]
    public TimeSpan StartDate { get; set; } = TimeSpan.Zero;

    [DataField, AutoNetworkedField]
    public string? Title { get; set; }
    [DataField, AutoNetworkedField]
    public string? Desc { get; set; }
    [DataField, AutoNetworkedField]
    public string? Rule { get; set; }

    public override bool SendOnlyToOwner => true;
}
