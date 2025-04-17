using System.Text.Json.Serialization;
using Content.Shared.Backmen.Surgery.Consciousness;
using Content.Shared.Backmen.Surgery.Consciousness.Systems;
using Content.Shared.Backmen.Surgery.Pain.Systems;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.EntityEffects;
using Content.Shared.FixedPoint;
using JetBrains.Annotations;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Server.EntityEffects.Effects;

[UsedImplicitly]
public sealed partial class AdjustConsciousness : EntityEffect // backmen effect
{
    [DataField(required: true)]
    [JsonPropertyName("amount")]
    public FixedPoint2 Amount = default!;

    [DataField(required: true)]
    [JsonPropertyName("time")]
    public TimeSpan Time = default!;

    [DataField]
    [JsonPropertyName("identifier")]
    public string Identifier = "ConsciousnessModifier";

    [DataField("allowNewModifiers")]
    [JsonPropertyName("allowNewModifiers")]
    public bool AllowCreatingModifiers = true;

    [DataField]
    [JsonPropertyName("modifierType")]
    public ConsciousnessModType ModifierType = ConsciousnessModType.Generic;

    protected override string? ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
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
                    .EditConsciousnessModifier(args.TargetEntity,
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
                .EditConsciousnessModifier(args.TargetEntity, nerveSys.Value.Owner, Amount * scale, Identifier, Time);
        }
    }
}
