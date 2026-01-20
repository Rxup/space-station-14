using Robust.Shared.Prototypes;
using Content.Shared.Backmen.Disease.Effects;

namespace Content.Shared.EntityEffects.Effects;

/// <summary>
/// Removes DiseaseLubeHandsComponent from the entity
/// </summary>
/// <inheritdoc cref="EntityEffectSystem{T,TEffect}"/>
public sealed partial class RemoveDiseaseLubeHandsEntityEffectSystem : EntityEffectSystem<DiseaseLubeHandsComponent, RemoveDiseaseLubeHandsEntityEffect>
{
    protected override void Effect(Entity<DiseaseLubeHandsComponent> entity, ref EntityEffectEvent<RemoveDiseaseLubeHandsEntityEffect> args)
    {
        RemCompDeferred<DiseaseLubeHandsComponent>(entity);
    }
}

/// <inheritdoc cref="EntityEffect"/>
public sealed partial class RemoveDiseaseLubeHandsEntityEffect : EntityEffectBase<RemoveDiseaseLubeHandsEntityEffect>
{
    public override string? EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys) => null;
}
