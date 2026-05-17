using Robust.Shared.GameStates;

namespace Content.Shared._Impstation.Revenant.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class RevealRevenantOnCollideComponent : Component
{
    [DataField, AutoNetworkedField]
    public string PopupText = "revenant-revealed-default";

    [DataField, AutoNetworkedField]
    public TimeSpan RevealTime = TimeSpan.FromSeconds(5);

    [DataField, AutoNetworkedField]
    public TimeSpan? StunTime;
}
