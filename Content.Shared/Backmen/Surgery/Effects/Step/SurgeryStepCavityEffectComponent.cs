using Robust.Shared.GameStates;

namespace Content.Shared.Backmen.Surgery.Effects.Step;

[RegisterComponent, NetworkedComponent]
public sealed partial class SurgeryStepCavityEffectComponent : Component
{
    public const string SlotId = "cavity_slot";

    [DataField]
    public string Action = "Insert";
}
