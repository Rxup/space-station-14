using Content.Shared.Backmen.Disease;
using Content.Shared.Database;
using Content.Shared.EntityEffects;
using Content.Shared.Mobs.Components;
using JetBrains.Annotations;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Shared.Backmen.EntityEffects.Effects;

/// <summary>
/// Default metabolism for medicine reagents.
/// </summary>
/// <inheritdoc cref="EntityEffectSystem{T, TEffect}"/>
[UsedImplicitly]
public sealed partial class ChemCauseDiseaseEntityEffectSystem : EntityEffectSystem<MobStateComponent, ChemCauseDisease>
{
    [Dependency] private readonly SharedDiseaseSystem _disease = default!;

    protected override void Effect(Entity<MobStateComponent> entity, ref EntityEffectEvent<ChemCauseDisease> args)
    {
        if (args.Scale != 1f)
            return;

        _disease.TryAddDisease(entity, args.Effect.Disease);
    }
}

[UsedImplicitly]
public sealed partial class ChemCauseDisease : EntityEffectBase<ChemCauseDisease>
{
    public override LogImpact? Impact => LogImpact.Medium;

    public override string? EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => Loc.GetString("reagent-effect-guidebook-chem-cause-disease", ("chance", Probability),
            ("disease", prototype.Index<DiseasePrototype>(Disease).Name));

    /// <summary>
    /// Chance it has each tick to cause disease, between 0 and 1
    /// </summary>
    [DataField]
    public float CauseChance = 0.15f;

    /// <summary>
    /// The disease to add.
    /// </summary>
    [DataField(customTypeSerializer: typeof(PrototypeIdSerializer<DiseasePrototype>), required: true)]
    public string Disease = default!;
}
