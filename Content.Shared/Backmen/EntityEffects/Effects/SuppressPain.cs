using Content.Shared.Backmen.Surgery.Consciousness.Systems;
using Content.Shared.Backmen.Surgery.Pain.Systems;
using Content.Shared.Body.Part;
using Content.Shared.Backmen.Body.Systems;
using Content.Shared.EntityEffects;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs.Components;
using JetBrains.Annotations;
using Robust.Shared.Prototypes;

namespace Content.Shared.Backmen.EntityEffects.Effects;

/// <inheritdoc cref="EntityEffectSystem{T, TEffect}"/>
[UsedImplicitly]
public sealed partial class SuppressPainEntityEffectSystem : EntityEffectSystem<MobStateComponent, SuppressPain>
{
    [Dependency] private ConsciousnessSystem _consciousness = default!;
    [Dependency] private BkmBodySharedSystem _body = default!;
    [Dependency] private PainSystem _pain = default!;

    protected override void Effect(Entity<MobStateComponent> entity, ref EntityEffectEvent<SuppressPain> args)
    {
        var scale = FixedPoint2.New(args.Scale);

        if (!_consciousness.TryGetNerveSystem(entity.Owner, out var nerveSys))
            return;

        if (!_body.TryGetWoundableTargetByType(entity, BodyPartType.Head, null, out var headId))
            return;

        if (!_pain.TryGetPainModifier(nerveSys.Value, headId, args.Effect.ModifierIdentifier, out var modifier))
        {
            _pain.TryAddPainModifier(
                    nerveSys.Value,
                    headId,
                    args.Effect.ModifierIdentifier,
                    FixedPoint2.Clamp(-args.Effect.Amount * scale, -args.Effect.MaximumSuppression, args.Effect.MaximumSuppression),
                    time: args.Effect.Time);
        }
        else
        {
            _pain.TryChangePainModifier(
                    nerveSys.Value,
                    headId,
                    args.Effect.ModifierIdentifier,
                    FixedPoint2.Clamp(modifier.Value.Change - args.Effect.Amount * scale, -args.Effect.MaximumSuppression, args.Effect.MaximumSuppression),
                    time: args.Effect.Time);
        }
    }
}

[UsedImplicitly]
public sealed partial class SuppressPain : EntityEffectBase<SuppressPain>
{
    [DataField(required: true)]
    public FixedPoint2 Amount;

    [DataField(required: true)]
    public TimeSpan Time;

    [DataField]
    public FixedPoint2 MaximumSuppression = 80f;

    [DataField]
    public string ModifierIdentifier = "PainSuppressant";

    public override string? EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => Loc.GetString(
            "entity-effect-guidebook-suppress-pain",
            ("chance", Probability),
            ("amount", Amount.Float()),
            ("time", Time.TotalSeconds),
            ("maximumSuppression", MaximumSuppression.Float()));
}
