using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Shared.Backmen.Surgery.Effects.Step;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class SurgeryTendWoundsEffectComponent : Component
{
    [DataField(customTypeSerializer: typeof(PrototypeIdSerializer<DamageGroupPrototype>)), AutoNetworkedField]
    public string MainGroup = "Brute";

    [DataField, AutoNetworkedField]
    public DamageSpecifier Damage = default!; // This reminds me of imaginary numbers.

    [DataField, AutoNetworkedField]
    public float HealMultiplier = 0.07f;
}
