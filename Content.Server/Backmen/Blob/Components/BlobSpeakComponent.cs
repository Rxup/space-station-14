using Content.Shared.Radio;
using Robust.Shared.Prototypes;

namespace Content.Server.Backmen.Blob.Components;

[RegisterComponent]
public sealed partial class BlobSpeakComponent : Component
{
    [DataField]
    public ProtoId<RadioChannelPrototype> Channel = "Hivemind";

    /// <summary>
    /// Hide entity name
    /// </summary>
    [DataField]
    public bool OverrideName = true;

    [DataField]
    public LocId Name = "speak-vv-blob";

    /// <summary>
    /// Duplicate all your speak into radio channel
    /// </summary>
    [DataField]
    public bool LongRange = true;
}
