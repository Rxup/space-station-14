using Content.Shared.Backmen.Psionics;
using Content.Shared.Backmen.Psionics.Components;
using Content.Shared.Database;
using Content.Shared.EntityEffects;
using JetBrains.Annotations;
using Robust.Shared.Prototypes;

namespace Content.Shared.Backmen.EntityEffects.Effects;

/// <summary>
/// Rerolls psionics once.
/// </summary>
/// <inheritdoc cref="EntityEffectSystem{T, TEffect}"/>
[UsedImplicitly]
public sealed partial class ChemRerollPsionicEntityEffectSystem : EntityEffectSystem<PotentialPsionicComponent, ChemRerollPsionic>
{
    [Dependency] private readonly SharedPsionicsSystem _psionics = default!;

    protected override void Effect(Entity<PotentialPsionicComponent> entity, ref EntityEffectEvent<ChemRerollPsionic> args)
    {
        _psionics.RerollPsionics(entity.AsNullable(), bonusMuliplier: args.Effect.BonusMuliplier);
    }
}

[UsedImplicitly]
public sealed partial class ChemRerollPsionic : EntityEffectBase<ChemRerollPsionic>
{
    public override LogImpact? Impact => LogImpact.Medium;

    public override string? EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => Loc.GetString("reagent-effect-guidebook-chem-reroll-psionic", ("chance", Probability));

    /// <summary>
    /// Reroll multiplier.
    /// </summary>
    [DataField("bonusMultiplier")]
    public float BonusMuliplier = 1f;
}
