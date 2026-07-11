using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.ViewVariables;

namespace Content.Shared.Backmen.Targeting;

/// <summary>
/// Overrides combat spread tier resolution for this entity (implant, admin ghost, VV).
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CombatTargetOddsOverrideComponent : Component
{
    [ViewVariables(VVAccess.ReadWrite)]
    [DataField(required: true)]
    public ProtoId<CombatTargetOddsPrototype> Odds = "Security";

    /// <summary>
    /// When true, the override was applied by a combat training implant and may be removed on extract.
    /// </summary>
    [DataField]
    public bool FromImplant;
}
