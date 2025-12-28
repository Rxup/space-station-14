using Robust.Shared.GameStates;

namespace Content.Shared._Backmen.Surgery.Effects.Step;

[RegisterComponent, NetworkedComponent]
public sealed partial class SurgeryStepCavityEffectComponent : Component
{
    [DataField]
    public string Action = "Insert";
}
