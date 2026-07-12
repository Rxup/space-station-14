using Robust.Shared.Prototypes;
using Robust.Shared.ViewVariables;

namespace Content.Shared.Backmen.Targeting;

[RegisterComponent]
public sealed partial class CombatTrainingImplantComponent : Component
{
    [ViewVariables(VVAccess.ReadWrite)]
    [DataField(required: true)]
    public ProtoId<CombatTargetOddsPrototype> Odds = "Security";
}
