using Content.Shared._Backmen.Flesh;

namespace Content.Client._Backmen.Flesh;

[RegisterComponent]
public sealed partial class FleshHeartComponent : Component
{
    [DataField("finalState")]
    public string? FinalState = "underpowered";
}
