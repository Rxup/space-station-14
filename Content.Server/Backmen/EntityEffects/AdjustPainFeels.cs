using Content.Server.Body.Systems;
using Content.Shared.Backmen.Surgery.Consciousness.Systems;
using Content.Shared.Backmen.Surgery.Pain.Systems;
using Content.Shared.EntityEffects;
using Content.Shared.FixedPoint;
using JetBrains.Annotations;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.Backmen.EntityEffects;

[UsedImplicitly]
public sealed partial class AdjustPainFeels : EntityEffect
{
    [DataField(required: true)]
    public FixedPoint2 Amount;

    [DataField("randomise")]
    public bool RandomiseAmount = true;

    [DataField]
    public string ModifierIdentifier = "PainSuppressant";

    protected override string? ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys) => null;

    public override void Effect(EntityEffectBaseArgs args)
    {
        var scale = FixedPoint2.New(1);

        if (args is EntityEffectReagentArgs reagentArgs)
        {
            scale = reagentArgs.Quantity * reagentArgs.Scale;
        }

        if (!args.EntityManager.System<ConsciousnessSystem>().TryGetNerveSystem(args.TargetEntity, out var nerveSys))
            return;

        var random = IoCManager.Resolve<IRobustRandom>();
        foreach (var bodyPart in args.EntityManager.System<BodySystem>().GetBodyChildren(args.TargetEntity))
        {
            if (!args.EntityManager.System<PainSystem>()
                    .TryGetPainFeelsModifier(bodyPart.Id, nerveSys.Value, ModifierIdentifier, out var modifier))
            {
                var add = Amount;
                if (RandomiseAmount && random.Prob(0.3f))
                    add = -add;

                args.EntityManager.System<PainSystem>()
                    .TryAddPainFeelsModifier(
                        nerveSys.Value,
                        ModifierIdentifier,
                        bodyPart.Id,
                        add);
            }
            else
            {
                var add = Amount;
                if (RandomiseAmount && random.Prob(0.3f))
                    add = -add;

                args.EntityManager.System<PainSystem>()
                    .TryChangePainFeelsModifier(
                        nerveSys.Value,
                        ModifierIdentifier,
                        bodyPart.Id,
                        add * scale);
            }
        }
    }
}
