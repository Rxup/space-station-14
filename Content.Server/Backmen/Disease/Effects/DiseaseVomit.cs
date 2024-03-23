using Content.Server.Medical;
using Content.Shared.Backmen.Disease;
using JetBrains.Annotations;
using Robust.Shared.Prototypes;

namespace Content.Server.Backmen.Disease.Effects;

/// <summary>
/// Forces you to vomit.
/// </summary>
[UsedImplicitly]
public sealed partial class DiseaseVomit : DiseaseEffect
{
    /// How many units of thirst to add each time we vomit
    [DataField("thirstAmount")]
    public float ThirstAmount = -40f;
    /// How many units of hunger to add each time we vomit
    [DataField("hungerAmount")]
    public float HungerAmount = -40f;

    public override object GenerateEvent(Entity<DiseaseCarrierComponent> ent, ProtoId<DiseasePrototype> disease)
    {
        return new DiseaseEffectArgs<DiseaseVomit>(ent, disease, this);
    }
}
public sealed partial class DiseaseEffectSystem
{
    [Dependency] private readonly VomitSystem _vomit = default!;
    private void DiseaseVomit(Entity<DiseaseCarrierComponent> ent, ref DiseaseEffectArgs<DiseaseVomit> args)
    {
        if(args.Handled)
            return;
        args.Handled = true;
        _vomit.Vomit(args.DiseasedEntity, args.DiseaseEffect.ThirstAmount, args.DiseaseEffect.HungerAmount);
    }
}
