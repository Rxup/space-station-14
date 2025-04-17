using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;

namespace Content.Shared.Backmen.Surgery.Steps;

[RegisterComponent, NetworkedComponent]
public sealed partial class SurgeryBleedsTreatmentStepComponent : Component
{
    [DataField]
    public FixedPoint2 Amount = 5;
}
