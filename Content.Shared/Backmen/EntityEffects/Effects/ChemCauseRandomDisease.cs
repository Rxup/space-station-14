using Content.Shared.Backmen.Disease;
using Content.Shared.Database;
using Content.Shared.EntityEffects;
using Content.Shared.Mobs.Components;
using JetBrains.Annotations;
using Robust.Shared.Prototypes;

namespace Content.Shared.Backmen.EntityEffects.Effects;

/// <summary>
/// Infects with the current entry of a named disease pool (rotates over time).
/// Does not stack: if the target is already diseased, does nothing.
/// </summary>
/// <inheritdoc cref="EntityEffectSystem{T, TEffect}"/>
[UsedImplicitly]
public sealed partial class ChemCauseRandomDiseaseEntityEffectSystem : EntityEffectSystem<MobStateComponent, ChemCauseRandomDisease>
{
    [Dependency] private SharedBkRottingSystem _rotting = default!;
    [Dependency] private SharedDiseaseSystem _disease = default!;

    protected override void Effect(Entity<MobStateComponent> entity, ref EntityEffectEvent<ChemCauseRandomDisease> args)
    {
        if (args.Scale != 1f)
            return;

        if (HasComp<DiseasedComponent>(entity))
            return;

        var disease = _rotting.RequestPoolDisease(args.Effect.Pool);
        if (disease is not { } diseaseId)
            return;

        _disease.TryAddDisease(entity, diseaseId);
    }
}

[UsedImplicitly]
public sealed partial class ChemCauseRandomDisease : EntityEffectBase<ChemCauseRandomDisease>
{
    public override LogImpact? Impact => LogImpact.Medium;

    /// <summary>
    /// Which rotating disease pool to use (see <see cref="SharedBkRottingSystem"/>).
    /// </summary>
    [DataField]
    public ProtoId<DiseasePoolPrototype> Pool = SharedBkRottingSystem.MoldPool;

    public override string? EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => Loc.GetString("reagent-effect-guidebook-chem-cause-random-disease", ("chance", Probability));
}
