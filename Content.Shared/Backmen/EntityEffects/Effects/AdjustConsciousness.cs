using Content.Shared.Backmen.Surgery.Consciousness;
using Content.Shared.Backmen.Surgery.Consciousness.Systems;
using Content.Shared.EntityEffects;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs.Components;
using JetBrains.Annotations;
using Robust.Shared.Prototypes;

namespace Content.Shared.Backmen.EntityEffects.Effects;

/// <inheritdoc cref="EntityEffectSystem{T, TEffect}"/>
public sealed partial class AdjustConsciousnessEntityEffectSystem : EntityEffectSystem<MobStateComponent, AdjustConsciousness>
{
    [Dependency] private readonly ConsciousnessSystem _consciousness = default!;

    protected override void Effect(Entity<MobStateComponent> entity, ref EntityEffectEvent<AdjustConsciousness> args)
    {
        var scale = FixedPoint2.New(args.Scale);

        if (!_consciousness.TryGetNerveSystem(entity.Owner, out var nerveSys))
            return;

        if (args.Effect.AllowCreatingModifiers)
        {
            if (!_consciousness.ChangeConsciousnessModifier(entity.Owner,
                    nerveSys.Value.Owner,
                    args.Effect.Amount * scale,
                    args.Effect.Identifier,
                    args.Effect.Time))
            {
                _consciousness.AddConsciousnessModifier(entity.Owner,
                    nerveSys.Value.Owner,
                    args.Effect.Amount * scale,
                    args.Effect.Identifier,
                    args.Effect.ModifierType,
                    args.Effect.Time);
            }
        }
        else
        {
            _consciousness.ChangeConsciousnessModifier(entity.Owner, nerveSys.Value.Owner, args.Effect.Amount * scale, args.Effect.Identifier, args.Effect.Time);
        }
    }
}

[UsedImplicitly]
public sealed partial class AdjustConsciousness : EntityEffectBase<AdjustConsciousness>
{
    [DataField(required: true)]
    public FixedPoint2 Amount;

    [DataField(required: true)]
    public TimeSpan Time;

    [DataField]
    public string Identifier = "ConsciousnessModifier";

    [DataField("allowNewModifiers")]
    public bool AllowCreatingModifiers = true;

    [DataField]
    public ConsciousnessModType ModifierType = ConsciousnessModType.Generic;

    public override string? EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => Loc.GetString("reagent-effect-guidebook-adjust-consciousness");
}
