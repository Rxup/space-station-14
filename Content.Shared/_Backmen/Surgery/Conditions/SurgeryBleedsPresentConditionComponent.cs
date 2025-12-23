using Robust.Shared.GameStates;

namespace Content.Shared._Backmen.Surgery.Conditions;

[RegisterComponent, NetworkedComponent]
public sealed partial class SurgeryBleedsPresentConditionComponent : Component
{
    [DataField]
    public bool Inverted = false;
}
