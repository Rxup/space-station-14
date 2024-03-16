using Content.Shared.Radio;
using Robust.Shared.Prototypes;

namespace Content.Server.Backmen.Blob.Components;

[RegisterComponent]
public sealed partial class BlobSpeakComponent : Component
{
    public ProtoId<RadioChannelPrototype> Channel = "Hivemind";
}
