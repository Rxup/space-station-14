namespace Content.Server._Cats.Slippery;

/// <summary>
///     Marks this item as one that may be dropped when its wearer slips with it equipped.
/// </summary>
[RegisterComponent]
public sealed partial class DropOnSlipComponent : Component
{
    /// <summary>
    ///     Percent chance to drop this item when slipping
    /// </summary>
    [DataField("chance")]
    [ViewVariables(VVAccess.ReadWrite)]
    public int Chance = 20;
}