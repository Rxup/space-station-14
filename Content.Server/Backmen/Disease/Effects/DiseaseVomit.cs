using Content.Shared.Backmen.Disease;
using Content.Shared.Medical;
using Content.Shared.Nutrition.Components;
using Content.Shared.Nutrition.EntitySystems;
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
    public float ThirstAmount = -30f;
    /// How many units of hunger to add each time we vomit
    [DataField("hungerAmount")]
    public float HungerAmount = -30f;

    /// <summary>
    /// Minimum normalized hunger level (0–1, where 1 is full/Okay) required to reduce hunger when vomiting.
    /// Below this (or with no HungerComponent), vomiting still occurs but hunger is not reduced.
    /// Defaults to 0.5 (50% satiety).
    /// </summary>
    [DataField("minHungerLevel")]
    public float MinHungerLevel = 0.5f;

    public override object GenerateEvent(Entity<DiseaseCarrierComponent> ent, ProtoId<DiseasePrototype> disease)
    {
        return new DiseaseEffectArgs<DiseaseVomit>(ent, disease, this);
    }
}
public sealed partial class DiseaseEffectSystem
{
    [Dependency] private VomitSystem _vomit = default!;
    [Dependency] private HungerSystem _hunger = default!;

    private void DiseaseVomit(Entity<DiseaseCarrierComponent> ent, ref DiseaseEffectArgs<DiseaseVomit> args)
    {
        if (args.Handled)
            return;

        var hungerAmount = 0f;

        if (TryComp<HungerComponent>(args.DiseasedEntity, out var hunger)
            && (args.DiseaseEffect.MinHungerLevel <= 0f
                || _hunger.GetHungerLevel(hunger) >= args.DiseaseEffect.MinHungerLevel))
        {
            hungerAmount = args.DiseaseEffect.HungerAmount;
        }

        args.Handled = true;
        _vomit.Vomit(args.DiseasedEntity, args.DiseaseEffect.ThirstAmount, hungerAmount);
    }
}
