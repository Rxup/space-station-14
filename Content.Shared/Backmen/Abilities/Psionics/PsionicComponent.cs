using Content.Shared.Radio;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Shared.Backmen.Abilities.Psionics;

[RegisterComponent, NetworkedComponent]
public sealed partial class PsionicComponent : Component
{
    public EntityUid? PsionicAbility = null;

    /// <summary>
    ///     Ifrits, revenants, etc are explicitly magical beings that shouldn't get mindbreakered.
    /// </summary>
    [DataField("removable")]
    public bool Removable = true;

    [DataField("channel", customTypeSerializer: typeof(PrototypeIdSerializer<RadioChannelPrototype>))]
    public string? Channel;

    [DataField("channelColor")]
    public Color ChannelColor = Color.PaleVioletRed;
}
