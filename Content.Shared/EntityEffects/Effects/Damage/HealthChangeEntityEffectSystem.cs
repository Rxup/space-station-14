using System.Linq;
using Content.Shared.Backmen.Targeting;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Localizations;
using Robust.Shared.Prototypes;

namespace Content.Shared.EntityEffects.Effects.Damage;

/// <summary>
/// Adjust the damages on this entity by specified amounts.
/// Amounts are modified by scale.
/// </summary>
/// <inheritdoc cref="EntityEffectSystem{T,TEffect}"/>
public sealed partial class HealthChangeEntityEffectSystem : EntityEffectSystem<DamageableComponent, HealthChange>
{
    [Dependency] private readonly DamageableSystem _damageable = default!;

    protected override void Effect(Entity<DamageableComponent> entity, ref EntityEffectEvent<HealthChange> args)
    {
        var damageSpec = new DamageSpecifier(args.Effect.Damage);

        damageSpec *= args.Scale;

        _damageable.TryChangeDamage(
                entity.AsNullable(),
                damageSpec,
                out _,
                args.Effect.IgnoreResistances,
                interruptsDoAfters: false,
                ignoreGlobalModifiers: false,
                partMultiplier: 1.00f,
                targetPart: args.Effect.TargetPart); // backmen
    }
}

/// <inheritdoc cref="EntityEffect"/>
public sealed partial class HealthChange : EntityEffectBase<HealthChange>
{
    /// <summary>
    /// Damage to apply every cycle. Damage Ignores resistances.
    /// </summary>
    [DataField(required: true)]
    public DamageSpecifier Damage = default!;

    [DataField]
    public bool IgnoreResistances = true;

    [DataField]
    public TargetBodyPart TargetPart = TargetBodyPart.All; // backmen

    public override string EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        {
            var damages = new List<string>();
            var heals = false;
            var deals = false;

            var damageSpec = new DamageSpecifier(Damage);

            var universalReagentDamageModifier = entSys.GetEntitySystem<DamageableSystem>().UniversalReagentDamageModifier;
            var universalReagentHealModifier = entSys.GetEntitySystem<DamageableSystem>().UniversalReagentHealModifier;

            damageSpec = entSys.GetEntitySystem<DamageableSystem>().ApplyUniversalAllModifiers(damageSpec);

            foreach (var (kind, amount) in damageSpec.DamageDict)
            {
                var sign = FixedPoint2.Sign(amount);
                float mod;

                switch (sign)
                {
                    case < 0:
                        heals = true;
                        mod = universalReagentHealModifier;
                        break;
                    case > 0:
                        deals = true;
                        mod = universalReagentDamageModifier;
                        break;
                    default:
                        continue; // Don't need to show damage types of 0...
                }

                damages.Add(
                    Loc.GetString("health-change-display",
                        ("kind", prototype.Index<DamageTypePrototype>(kind).LocalizedName),
                        ("amount", MathF.Abs(amount.Float() * mod)),
                        ("deltasign", sign)
                    ));
            }

            var healsordeals = heals ? (deals ? "both" : "heals") : (deals ? "deals" : "none");

            // start-backmen
            // Format target body part if not All
            string? targetPartText = null;
            if (TargetPart != TargetBodyPart.All)
            {
                targetPartText = FormatTargetBodyPart(TargetPart);
            }
            // end-backmen

            return Loc.GetString("entity-effect-guidebook-health-change",
                ("chance", Probability),
                ("changes", ContentLocalizationManager.FormatList(damages)),
                ("healsordeals", healsordeals),
                ("targetPart", targetPartText ?? "")); // backmen
    }

    private static string FormatTargetBodyPart(TargetBodyPart targetPart)
    {
        // Check for composite values first (exact matches)
        var compositeName = targetPart switch
        {
            TargetBodyPart.LeftFullArm => "target-body-part-left-full-arm",
            TargetBodyPart.RightFullArm => "target-body-part-right-full-arm",
            TargetBodyPart.LeftFullLeg => "target-body-part-left-full-leg",
            TargetBodyPart.RightFullLeg => "target-body-part-right-full-leg",
            TargetBodyPart.Hands => "target-body-part-hands",
            TargetBodyPart.Arms => "target-body-part-arms",
            TargetBodyPart.Legs => "target-body-part-legs",
            TargetBodyPart.Feet => "target-body-part-feet",
            TargetBodyPart.FullArms => "target-body-part-full-arms",
            TargetBodyPart.FullLegs => "target-body-part-full-legs",
            TargetBodyPart.BodyMiddle => "target-body-part-body-middle",
            TargetBodyPart.FullLegsGroin => "target-body-part-full-legs-groin",
            _ => null
        };

        if (compositeName != null)
            return Loc.GetString(compositeName);

        // Handle individual flags
        var parts = new List<string>();
        var validParts = SharedTargetingSystem.GetValidParts();

        foreach (var part in validParts)
        {
            if (targetPart.HasFlag(part) && (int)targetPart != (int)(TargetBodyPart.All))
            {
                var partName = part switch
                {
                    TargetBodyPart.Head => "target-body-part-head",
                    TargetBodyPart.Chest => "target-body-part-chest",
                    TargetBodyPart.Groin => "target-body-part-groin",
                    TargetBodyPart.LeftArm => "target-body-part-left-arm",
                    TargetBodyPart.LeftHand => "target-body-part-left-hand",
                    TargetBodyPart.RightArm => "target-body-part-right-arm",
                    TargetBodyPart.RightHand => "target-body-part-right-hand",
                    TargetBodyPart.LeftLeg => "target-body-part-left-leg",
                    TargetBodyPart.LeftFoot => "target-body-part-left-foot",
                    TargetBodyPart.RightLeg => "target-body-part-right-leg",
                    TargetBodyPart.RightFoot => "target-body-part-right-foot",
                    _ => null
                };

                if (partName != null)
                    parts.Add(partName);
            }
        }

        // If we have specific parts, format them
        if (parts.Count > 0)
        {
            var localizedParts = parts.Select(p => Loc.GetString(p)).ToList();
            return ContentLocalizationManager.FormatList(localizedParts);
        }

        // Fallback
        return Enum.GetName(typeof(TargetBodyPart), targetPart) ?? "Unknown";
    }
}
