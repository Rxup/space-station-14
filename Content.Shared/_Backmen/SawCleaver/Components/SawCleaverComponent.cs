using Content.Shared.Damage;

namespace Content.Shared._Backmen.SawCleaver;

[RegisterComponent]
public sealed partial class SawCleaverComponent : Component
{
    [DataField]
    public DamageSpecifier HealOnHit = new();
}
