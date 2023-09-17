using Content.Shared.Damage;

namespace Content.Server.Backmen.Abilities.Oni;

[RegisterComponent]
public sealed partial class OniComponent : Component
{
    [DataField("modifiers", required: true)]
    public DamageModifierSet MeleeModifiers = default!;

    [DataField("stamDamageBonus")]
    public float StamDamageMultiplier = 1.25f;
}
