using Content.Shared.Backmen.Surgery.Traumas;
using Content.Shared.Body.Part;
using Robust.Shared.GameStates;

namespace Content.Shared.Backmen.Surgery.Conditions;

[RegisterComponent, NetworkedComponent]
public sealed partial class SurgeryTraumaPresentConditionComponent : Component
{
    [DataField("trauma")]
    public TraumaType TraumaType = TraumaType.BoneDamage;

    [DataField]
    public bool Inverted = false;
}
