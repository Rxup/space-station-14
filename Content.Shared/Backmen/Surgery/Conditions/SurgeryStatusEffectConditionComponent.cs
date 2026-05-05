using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Backmen.Surgery.Conditions;

[RegisterComponent, NetworkedComponent]
public sealed partial class SurgeryStatusEffectConditionComponent : Component
{
    [DataField]
    public HashSet<EntProtoId> StatusEffects = new();

    [DataField]
    public bool Inverse;
}
