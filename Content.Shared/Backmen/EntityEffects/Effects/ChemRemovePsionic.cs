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
public sealed partial class ChemRemovePsionicEntityEffectSystem : EntityEffectSystem<PotentialPsionicComponent, ChemRemovePsionic>
{
    [Dependency] private readonly SharedPsionicsSystem _psionics = default!;

    protected override void Effect(Entity<PotentialPsionicComponent> entity, ref EntityEffectEvent<ChemRemovePsionic> args)
    {
        if (args.Scale != 1f)
            return;

        _psionics.RemovePsionics(entity.AsNullable());
    }
}

[UsedImplicitly]
public sealed partial class ChemRemovePsionic : EntityEffectBase<ChemRemovePsionic>
{
    public override LogImpact? Impact => LogImpact.Medium;

    public override string? EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => Loc.GetString("reagent-effect-guidebook-chem-remove-psionic", ("chance", Probability));
}
