using Content.Shared.Backmen.Psionics.Glimmer;
using Content.Shared.EntityEffects;
using Content.Shared.Mobs.Components;
using Robust.Shared.Prototypes;

namespace Content.Shared.Backmen.EntityEffects.Effects;

/// <inheritdoc cref="EntityEffectSystem{T, TEffect}"/>
[DataDefinition]
public sealed partial class ChangeGlimmerReactionEffectEntityEffectSystem : EntityEffectSystem<MobStateComponent, ChangeGlimmerReactionEffect>
{
    [Dependency] private readonly GlimmerSystem _glimmer = default!;

    protected override void Effect(Entity<MobStateComponent> entity, ref EntityEffectEvent<ChangeGlimmerReactionEffect> args)
    {
        _glimmer.Glimmer += args.Effect.Count;
    }
}

[DataDefinition]
public sealed partial class ChangeGlimmerReactionEffect : EntityEffectBase<ChangeGlimmerReactionEffect>
{
    public override string? EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => Loc.GetString("reagent-effect-guidebook-change-glimmer-reaction-effect", ("chance", Probability),
            ("count", Count));

    /// <summary>
    ///     Added to glimmer when reaction occurs.
    /// </summary>
    [DataField("count")]
    public int Count = 1;
}
