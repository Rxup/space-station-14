using Robust.Shared.GameStates;

namespace Content.Shared.Medical.Surgery.Effects.Step;

[RegisterComponent, NetworkedComponent]
public sealed partial class SurgerySpecialDamageChangeEffectComponent : Component
{
    [DataField]
    public string DamageType = "";

    [DataField]
    public bool IsConsumable;
}