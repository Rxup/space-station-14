using Robust.Shared.GameStates;

namespace Content.Shared.Backmen.CollectiveMind;

[RegisterComponent, NetworkedComponent]
public sealed partial class CollectiveMindComponent : Component
{
    [DataField("channel", required: true)]
    public string Channel = string.Empty;

    [DataField("channelColor")]
    public Color ChannelColor = Color.Lime;
}
