using Content.Shared.Backmen.Flesh;

namespace Content.Client.Backmen.Flesh;

[RegisterComponent]
public sealed partial class FleshHeartComponent : Component
{
    [DataField("finalState")]
    public string? FinalState = "underpowered";
}
