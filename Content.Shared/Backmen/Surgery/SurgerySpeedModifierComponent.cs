using Robust.Shared.GameStates;

namespace Content.Shared.Backmen.Surgery;

[RegisterComponent, NetworkedComponent]
public sealed partial class SurgerySpeedModifierComponent : Component
{
    [DataField]
    public float SpeedModifier = 1.5f;
}
