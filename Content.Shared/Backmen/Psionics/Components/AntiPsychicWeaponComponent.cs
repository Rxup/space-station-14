using Content.Shared.Damage;
using Robust.Shared.GameStates;

namespace Content.Shared.Backmen.Psionics.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class AntiPsionicWeaponComponent : Component
{

    [DataField("modifiers", required: true)]
    public DamageModifierSet Modifiers = default!;

    [DataField("psychicStaminaDamage")]
    public float PsychicStaminaDamage = 30f;

    [DataField("disableChance")]
    public float DisableChance = 0.3f;

    /// <summary>
    ///     Punish when used against a non-psychic.
    /// </summary
    [DataField("punish")]
    public bool Punish = true;
}

