using System.Text.Json.Serialization;
using Content.Shared.Backmen.Surgery.Consciousness;
using Content.Shared.Backmen.Surgery.Consciousness.Systems;
using Content.Shared.EntityEffects;
using Content.Shared.FixedPoint;
using JetBrains.Annotations;
using Robust.Shared.Prototypes;

namespace Content.Server.Backmen.EntityEffects;

[UsedImplicitly]
public sealed partial class AdjustConsciousness : EntityEffect
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

    protected override string ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => Loc.GetString("reagent-effect-guidebook-adjust-consciousness");

    public override void Effect(EntityEffectBaseArgs args)
    {
        var scale = FixedPoint2.New(1);

        if (args is EntityEffectReagentArgs reagentArgs)
        {
            scale = reagentArgs.Quantity * reagentArgs.Scale;
        }

        if (!args.EntityManager.System<ConsciousnessSystem>().TryGetNerveSystem(args.TargetEntity, out var nerveSys))
            return;

        if (AllowCreatingModifiers)
        {
            if (!args.EntityManager.System<ConsciousnessSystem>()
                    .ChangeConsciousnessModifier(args.TargetEntity,
                        nerveSys.Value.Owner,
                        Amount * scale,
                        Identifier,
                        Time))
            {
                args.EntityManager.System<ConsciousnessSystem>()
                    .AddConsciousnessModifier(args.TargetEntity,
                        nerveSys.Value.Owner,
                        Amount * scale,
                        Identifier,
                        ModifierType,
                        Time);
            }
        }
        else
        {
            args.EntityManager.System<ConsciousnessSystem>()
                .ChangeConsciousnessModifier(args.TargetEntity, nerveSys.Value.Owner, Amount * scale, Identifier, Time);
        }
    }
}
