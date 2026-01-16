using Content.Shared.Backmen.Surgery.Consciousness.Systems;
using Content.Shared.Backmen.Surgery.Pain.Systems;
using Content.Shared.Body.Systems;
using Content.Shared.EntityEffects;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs.Components;
using JetBrains.Annotations;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Shared.Backmen.EntityEffects.Effects;

/// <inheritdoc cref="EntityEffectSystem{T, TEffect}"/>
[UsedImplicitly]
public sealed partial class AdjustPainFeelsEntityEffectSystem : EntityEffectSystem<MobStateComponent, AdjustPainFeels>
{
    [Dependency] private readonly ConsciousnessSystem _consciousness = default!;
    [Dependency] private readonly SharedBodySystem _body = default!;
    [Dependency] private readonly PainSystem _pain = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    protected override void Effect(Entity<MobStateComponent> entity, ref EntityEffectEvent<AdjustPainFeels> args)
    {
        var scale = FixedPoint2.New(args.Scale);

        if (!_consciousness.TryGetNerveSystem(entity.Owner, out var nerveSys))
            return;

        foreach (var bodyPart in _body.GetBodyChildren(entity))
        {
            if (!_pain.TryGetPainFeelsModifier(bodyPart.Id, nerveSys.Value, args.Effect.ModifierIdentifier, out var modifier))
            {
                var add = args.Effect.Amount;
                if (args.Effect.RandomiseAmount && _random.Prob(0.3f))
                    add = -add;

                _pain.TryAddPainFeelsModifier(
                        nerveSys.Value,
                        args.Effect.ModifierIdentifier,
                        bodyPart.Id,
                        add);
            }
            else
            {
                var add = args.Effect.Amount;
                if (args.Effect.RandomiseAmount && _random.Prob(0.3f))
                    add = -add;

                _pain.TryChangePainFeelsModifier(
                        nerveSys.Value,
                        args.Effect.ModifierIdentifier,
                        bodyPart.Id,
                        add * scale);
            }
        }
    }
}

[UsedImplicitly]
public sealed partial class AdjustPainFeels : EntityEffectBase<AdjustPainFeels>
{
    [DataField(required: true)]
    public FixedPoint2 Amount;

    [DataField("randomise")]
    public bool RandomiseAmount = true;

    [DataField]
    public string ModifierIdentifier = "PainSuppressant";

    public override string? EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys) => null;
}
