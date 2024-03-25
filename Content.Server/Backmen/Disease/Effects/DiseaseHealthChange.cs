using Content.Shared.Backmen.Disease;
using Content.Shared.Damage;
using JetBrains.Annotations;
using Robust.Shared.Prototypes;

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

    public override object GenerateEvent(Entity<DiseaseCarrierComponent> ent, ProtoId<DiseasePrototype> disease)
    {
        return new DiseaseEffectArgs<DiseaseHealthChange>(ent, disease, this);
    }
}

public sealed partial class DiseaseEffectSystem
{
    [Dependency] private readonly DamageableSystem _damageable = default!;

    private void DiseaseHealthChange(Entity<DiseaseCarrierComponent> ent, ref DiseaseEffectArgs<DiseaseHealthChange> args)
    {
        if(args.Handled)
            return;
        args.Handled = true;
        _damageable.TryChangeDamage(args.DiseasedEntity, args.DiseaseEffect.Damage, true, false);
    }
}
