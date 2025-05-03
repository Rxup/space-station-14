using Content.Server.Body.Components;
using Content.Server.Body.Systems;
using Content.Shared.Backmen.Surgery.Consciousness.Components;
using Content.Shared.Backmen.Surgery.Traumas.Components;
using Content.Shared.Backmen.Surgery.Wounds.Components;
using Content.Shared.Backmen.Surgery.Wounds.Systems;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.EntityEffects;
using Robust.Shared.Prototypes;

namespace Content.Server.EntityEffects.Effects;

public sealed partial class ModifyBleedAmount : EntityEffect
{
    [DataField]
    public bool Scaled = false;

    [DataField]
    public float Amount = -1.0f;

    protected override string? ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => Loc.GetString("reagent-effect-guidebook-modify-bleed-amount", ("chance", Probability),
            ("deltasign", MathF.Sign(Amount)));

    public override void Effect(EntityEffectBaseArgs args)
    {
        if (args.EntityManager.TryGetComponent<BloodstreamComponent>(args.TargetEntity, out var blood))
        {
            var sys = args.EntityManager.System<BloodstreamSystem>();
            var amt = Amount;
            if (args is EntityEffectReagentArgs reagentArgs) {
                if (Scaled)
                    amt *= reagentArgs.Quantity.Float();
                amt *= reagentArgs.Scale.Float();
            }

            // backmen edit start
            if (args.EntityManager.TryGetComponent<BodyComponent>(args.TargetEntity, out var body)
                && args.EntityManager.HasComponent<ConsciousnessComponent>(args.TargetEntity))
            {
                var bodySystem = args.EntityManager.System<SharedBodySystem>();
                var woundSystem = args.EntityManager.System<WoundSystem>();
                foreach (var bodyPart in bodySystem.GetBodyChildren(args.TargetEntity, body))
                {
                    if (!args.EntityManager.TryGetComponent<WoundableComponent>(bodyPart.Id, out var woundable))
                        continue;

                    foreach (var wound in woundSystem.GetWoundableWounds(bodyPart.Id, woundable))
                    {
                        if (!args.EntityManager.TryGetComponent<BleedInflicterComponent>(wound.Owner, out var bleeds))
                            continue;

                        if (bleeds.BleedingAmountRaw > amt)
                        {
                            bleeds.BleedingAmountRaw -= amt;
                            return;
                        }

                        bleeds.IsBleeding = false;

                        amt -= (float) bleeds.BleedingAmountRaw;
                        bleeds.BleedingAmountRaw = 0;
                    }
                }
            } // backmen edit end
            else
            {
                sys.TryModifyBleedAmount(args.TargetEntity, amt, blood);
            }
        }
    }
}
