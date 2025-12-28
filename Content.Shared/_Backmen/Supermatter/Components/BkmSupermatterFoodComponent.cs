namespace Content.Shared._Backmen.Supermatter.Components;

[RegisterComponent]
public sealed partial class BkmSupermatterFoodComponent : Component
{
    [ViewVariables(VVAccess.ReadWrite)]
    [DataField("energy")]
    public int Energy { get; set; } = 1;
}
