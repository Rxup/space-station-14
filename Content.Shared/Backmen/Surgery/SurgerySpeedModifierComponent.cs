using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Backmen.Surgery;

[RegisterComponent, NetworkedComponent]
public sealed partial class SurgerySpeedModifierComponent : Component
{
    [DataField]
    public float SpeedModifier = 1.5f;
}
