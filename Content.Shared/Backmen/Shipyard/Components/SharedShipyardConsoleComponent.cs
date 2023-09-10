using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared.Backmen.Shipyard.Components;

[NetworkedComponent]
public abstract partial class SharedShipyardConsoleComponent : Component
{
    [DataField("soundError")]
    public SoundSpecifier ErrorSound =
        new SoundPathSpecifier("/Audio/Effects/Cargo/buzz_sigh.ogg");

    [DataField("soundConfirm")]
    public SoundSpecifier ConfirmSound =
        new SoundPathSpecifier("/Audio/Effects/Cargo/ping.ogg");
}
