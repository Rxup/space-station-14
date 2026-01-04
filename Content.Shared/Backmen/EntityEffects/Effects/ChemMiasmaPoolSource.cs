using Content.Shared.Backmen.Disease;
using Content.Shared.EntityEffects;
using Content.Shared.Mobs.Components;
using JetBrains.Annotations;
using Robust.Shared.Prototypes;

namespace Content.Shared.Backmen.EntityEffects.Effects;

/// <summary>
/// The miasma system rotates between 1 disease at a time.
/// This gives all entities the disease the miasme system is currently on.
/// For things ingested by one person, you probably want ChemCauseRandomDisease instead.
/// </summary>
/// <inheritdoc cref="EntityEffectSystem{T, TEffect}"/>
[UsedImplicitly]
public sealed partial class ChemMiasmaPoolSourceEntityEffectSystem : EntityEffectSystem<MobStateComponent, ChemMiasmaPoolSource>
{
    [Dependency] private readonly SharedBkRottingSystem _rotting = default!;
    [Dependency] private readonly SharedDiseaseSystem _disease = default!;

    protected override void Effect(Entity<MobStateComponent> entity, ref EntityEffectEvent<ChemMiasmaPoolSource> args)
    {
        if (args.Scale != 1f)
            return;

        var disease = _rotting.RequestPoolDisease();
        _disease.TryAddDisease(entity, disease);
    }
}

[UsedImplicitly]
public sealed partial class ChemMiasmaPoolSource : EntityEffectBase<ChemMiasmaPoolSource>
{
    public override string? EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => Loc.GetString("reagent-effect-guidebook-chem-miasma-pool", ("chance", Probability));
}
