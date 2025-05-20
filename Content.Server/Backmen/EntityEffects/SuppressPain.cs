using System.Text.Json.Serialization;
using Content.Shared.Backmen.Surgery.Consciousness.Systems;
using Content.Shared.Backmen.Surgery.Pain.Systems;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.EntityEffects;
using Content.Shared.FixedPoint;
using JetBrains.Annotations;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Server.Backmen.EntityEffects;

[UsedImplicitly]
public sealed partial class SuppressPain : EntityEffect // backmen effect
{
    [DataField(required: true)]
    public FixedPoint2 Amount;

    [DataField(required: true)]
    public TimeSpan Time;

    [DataField]
    public FixedPoint2 MaximumSuppression = 80f;

    [DataField]
    public string ModifierIdentifier = "PainSuppressant";

    protected override string? ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => Loc.GetString("reagent-effect-guidebook-suppress-pain");

    public override void Effect(EntityEffectBaseArgs args)
    {
        var scale = FixedPoint2.New(1);

        if (args is EntityEffectReagentArgs reagentArgs)
        {
            scale = reagentArgs.Quantity * reagentArgs.Scale;
        }

        if (!args.EntityManager.System<ConsciousnessSystem>().TryGetNerveSystem(args.TargetEntity, out var nerveSys))
            return;

        var bodyPart = args.EntityManager.System<SharedBodySystem>()
            .GetBodyChildrenOfType(args.TargetEntity, BodyPartType.Head)
            .FirstOrNull();

        if (bodyPart == null)
            return;

        var painSystem = args.EntityManager.System<PainSystem>();
        if (!painSystem.TryGetPainModifier(nerveSys.Value, bodyPart.Value.Id, ModifierIdentifier, out var modifier))
        {
            painSystem.TryAddPainModifier(
                    nerveSys.Value,
                    bodyPart.Value.Id,
                    ModifierIdentifier,
                    FixedPoint2.Clamp(-Amount * scale, -MaximumSuppression, MaximumSuppression),
                    time: Time);
        }
        else
        {
            painSystem.TryChangePainModifier(
                    nerveSys.Value,
                    bodyPart.Value.Id,
                    ModifierIdentifier,
                    FixedPoint2.Clamp(modifier.Value.Change - Amount * scale, -MaximumSuppression, MaximumSuppression),
                    time: Time);
        }
    }
}
