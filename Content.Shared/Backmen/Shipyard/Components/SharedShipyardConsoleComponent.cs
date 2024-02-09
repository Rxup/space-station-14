using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared.Backmen.Shipyard.Components;

[NetworkedComponent, RegisterComponent]
public partial class ShipyardConsoleComponent : Component
{
    [DataField("soundError")]
    public SoundSpecifier ErrorSound =
        new SoundPathSpecifier("/Audio/Effects/Cargo/buzz_sigh.ogg");

    [DataField("soundConfirm")]
    public SoundSpecifier ConfirmSound =
        new SoundPathSpecifier("/Audio/Effects/Cargo/ping.ogg");

    [DataField("allowedGroup"), ViewVariables(VVAccess.ReadWrite)]
    public List<string> AllowedGroup = new List<string>();
}
