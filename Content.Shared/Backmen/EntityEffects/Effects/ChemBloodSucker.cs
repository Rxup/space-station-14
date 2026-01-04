using Content.Shared.Backmen.Vampiric;
using Content.Shared.Database;
using Content.Shared.EntityEffects;
using Content.Shared.Mobs.Components;
using Robust.Shared.Prototypes;

namespace Content.Shared.Backmen.EntityEffects.Effects;

/// <inheritdoc cref="EntityEffectSystem{T, TEffect}"/>
public sealed partial class ChemBloodSuckerEntityEffectSystem : EntityEffectSystem<MobStateComponent, ChemBloodSucker>
{
    [Dependency] private readonly SharedBloodSuckerSystem _bloodSucker = default!;

    protected override void Effect(Entity<MobStateComponent> entity, ref EntityEffectEvent<ChemBloodSucker> args)
    {
        if (args.Scale < 1f)
            return;

        _bloodSucker.ForceMakeVampire(entity);
    }
}

public sealed partial class ChemBloodSucker : EntityEffectBase<ChemBloodSucker>
{
    public override LogImpact? Impact => LogImpact.Medium;

    public override string? EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => Loc.GetString("reagent-effect-guidebook-missing", ("chance", Probability));
}
