using Content.Shared.Backmen.Disease;
using Content.Shared.Damage;
using JetBrains.Annotations;

namespace Content.Server.Backmen.Disease.Effects;

/// <summary>
/// Deals or heals damage to the host
/// </summary>
[UsedImplicitly]
public sealed partial class DiseaseHealthChange : DiseaseEffect
{
    [DataField("damage", required: true)]
    [ViewVariables(VVAccess.ReadWrite)]
    public DamageSpecifier Damage = default!;
}

public sealed partial class DiseaseEffectSystem
{
    [Dependency] private readonly DamageableSystem _damageable = default!;

    private void DiseaseHealthChange(DiseaseEffectArgs args, DiseaseHealthChange ds)
    {
        _damageable.TryChangeDamage(args.DiseasedEntity, ds.Damage, true, false);
    }
}
