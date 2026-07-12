using Content.Shared.Backmen.Targeting;

namespace Content.Shared.LandMines;

public sealed partial class LandMineComponent
{
    /// <summary>
    /// When set, explosion damage from this mine is routed to these body parts instead of random spread.
    /// </summary>
    [DataField]
    public TargetBodyPart? DamageTarget = TargetBodyPart.Legs;
}
