using Robust.Shared.GameStates;

namespace Content.Shared.Backmen.Surgery.Effects.Step;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class SurgeryRelieveStarvingPainEffectComponent : Component
{
    [DataField, AutoNetworkedField]
    public float PainReduction = 1f;
}
