using Robust.Shared.Prototypes;
using Content.Shared.StatusEffectNew.Components;

namespace Content.Shared.Backmen.Abilities.Psionics;

[RegisterComponent]
public sealed partial class ClothingGrantPsionicPowerComponent : Component
{
    [DataField("statusEffect", required: true)]
    public EntProtoId<StatusEffectComponent> StatusEffect;
    public bool _hasEffect = false;
}
