using System.Linq;
using System.Text;
using Content.Shared.Backmen.Surgery.Pain.Components;
using Content.Shared.Backmen.Surgery.Pain.Systems;
using Content.Shared.Backmen.Surgery.Traumas;
using Content.Shared.Backmen.Surgery.Traumas.Components;
using Content.Shared.Backmen.Surgery.Traumas.Systems;
using Content.Shared.Backmen.Surgery.Wounds.Components;
using Content.Shared.Body.Organ;
using Content.Shared.Body.Systems;
using Content.Shared.EntityEffects;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs.Components;
using JetBrains.Annotations;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Generic;
using Robust.Shared.Utility;

namespace Content.Shared.Backmen.EntityEffects.Effects;

/// <inheritdoc cref="EntityEffectSystem{T, TEffect}"/>
[UsedImplicitly]
public sealed partial class AdjustTraumasEntityEffectSystem : EntityEffectSystem<MobStateComponent, AdjustTraumas>
{
    [Dependency] private readonly SharedBodySystem _body = default!;
    [Dependency] private readonly TraumaSystem _trauma = default!;
    [Dependency] private readonly PainSystem _pain = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    private EntityQuery<WoundableComponent> _woundableQuery;
    private EntityQuery<OrganComponent> _organQuery;
    private EntityQuery<NerveComponent> _nerveQuery;
    private EntityQuery<BoneComponent> _boneQuery;

    public override void Initialize()
    {
        base.Initialize();
        _woundableQuery = GetEntityQuery<WoundableComponent>();
        _organQuery = GetEntityQuery<OrganComponent>();
        _nerveQuery = GetEntityQuery<NerveComponent>();
        _boneQuery = GetEntityQuery<BoneComponent>();
    }

    protected override void Effect(Entity<MobStateComponent> entity, ref EntityEffectEvent<AdjustTraumas> args)
    {
        var scale = FixedPoint2.New(args.Scale);
        var effect = args.Effect;
        var changeAmount = effect.Amount * scale;

        var possibleTraumaTargets = new HashSet<EntityUid>();

        if (effect.TargetBodyParts)
        {
            foreach (var uid in from bodyPart in _body.GetBodyChildren(entity)
                     where _woundableQuery.HasComponent(bodyPart.Id)
                     where CheckForTargetedComponents(bodyPart.Id, effect)
                     select bodyPart.Id)
            {
                possibleTraumaTargets.Add(uid);
            }
        }

        if (effect.TargetOrgans)
        {
            foreach (var uid in from organ in _body.GetBodyOrgans(entity)
                     where CheckForTargetedComponents(organ.Id, effect)
                     select organ.Id)
            {
                possibleTraumaTargets.Add(uid);
            }
        }

        if (possibleTraumaTargets.Count == 0)
            return;

        var target = _random.Pick(possibleTraumaTargets);
        ApplyTraumaEffects(entity, target, changeAmount, effect);
    }

    private void ApplyTraumaEffects(EntityUid body, EntityUid target, FixedPoint2 changeAmount, AdjustTraumas effect)
    {
        if (_organQuery.TryGetComponent(target, out var organComp))
        {
            if (effect.TraumaType != TraumaType.OrganDamage)
                return;

            if (!organComp.BodyPart.HasValue)
                return;

            ChangeOrganModifiers((target, organComp), changeAmount, effect);
        }
        else
        {
            switch (effect.TraumaType)
            {
                case TraumaType.BoneDamage:
                    var comp = _woundableQuery.GetComponent(target);
                    var bone = comp.Bone.ContainedEntities.FirstOrNull();
                    if (!_boneQuery.TryComp(bone, out var boneComp))
                        break;

                    _trauma.ApplyDamageToBone(bone.Value, changeAmount, boneComp);
                    break;
                case TraumaType.OrganDamage:
                    var organs = _body.GetPartOrgans(target).ToList();
                    if (organs.Count == 0)
                        break;

                    if (changeAmount > 0)
                    {
                        var organTarget = _random.PickAndTake(organs);
                        ChangeOrganModifiers(organTarget, changeAmount, effect);
                    }
                    else
                    {
                        var greatestDamage = FixedPoint2.Zero;
                        (EntityUid, OrganComponent)? organTarget = null;

                        foreach (var organ in organs)
                        {
                            var organDamage = organ.Component.IntegrityCap - organ.Component.OrganIntegrity;
                            if (organDamage <= greatestDamage)
                                continue;

                            greatestDamage = organDamage;
                            organTarget = organ;
                        }

                        if (organTarget != null)
                            ChangeOrganModifiers(organTarget.Value, changeAmount, effect);
                    }

                    break;
                case TraumaType.VeinsDamage:
                    // not implemented yet
                    break;
                case TraumaType.NerveDamage:
                    if (changeAmount > 0)
                    {
                        foreach (var bodyPart in _body.GetBodyChildren(body))
                        {
                            if (!_nerveQuery.TryComp(bodyPart.Id, out var nerve))
                                continue;

                            // you actually have AdjustPainFeels for this, but fine
                            if (!_pain.TryChangePainFeelsModifier(
                                    body,
                                    effect.ModifierIdentifier,
                                    bodyPart.Id,
                                    changeAmount,
                                    nerve))
                            {
                                _pain.TryAddPainFeelsModifier(body,
                                    effect.ModifierIdentifier,
                                    bodyPart.Id,
                                    changeAmount,
                                    nerve);
                            }
                        }
                    }
                    else
                    {
                        var available = changeAmount;
                        foreach (var bodyPart in _body.GetBodyChildren(body))
                        {
                            if (!_nerveQuery.TryComp(bodyPart.Id, out var nerve))
                                continue;

                            foreach (var painFeelsMod in nerve.PainFeelingModifiers)
                            {
                                var change = FixedPoint2.Abs(painFeelsMod.Value.Change);
                                if (change > available)
                                {
                                    var newChange = change - available;
                                    _pain.TrySetPainFeelsModifier(
                                        painFeelsMod.Key.Item1,
                                        painFeelsMod.Key.Item2,
                                        bodyPart.Id,
                                        newChange,
                                        nerve: nerve);
                                    break;
                                }

                                available -= change;
                                _pain.TryRemovePainFeelsModifier(
                                    painFeelsMod.Key.Item1,
                                    painFeelsMod.Key.Item2,
                                    bodyPart.Id,
                                    nerve);
                            }
                        }
                    }

                    break;
                case TraumaType.Dismemberment:
                    // not implemented yet
                    break;
            }
        }
    }

    private void ChangeOrganModifiers(Entity<OrganComponent> organ, FixedPoint2 changeAmount, AdjustTraumas effect)
    {
        if (changeAmount > 0)
        {
            if (!_trauma.TryChangeOrganDamageModifier(
                    organ.Owner,
                    changeAmount,
                    organ.Owner,
                    effect.ModifierIdentifier,
                    organ.Comp))
            {
                _trauma.TryAddOrganDamageModifier(
                    organ.Owner,
                    changeAmount,
                    organ.Owner,
                    effect.ModifierIdentifier,
                    organ.Comp);
            }
        }
        else
        {
            // Create a copy of the collection to avoid modifying it during iteration
            var modifiers = organ.Comp.IntegrityModifiers.ToList();
            var remainingHeal = -changeAmount; // Convert to positive value for easier comparison

            foreach (var modifier in modifiers)
            {
                // Healing modifier are not getting removed
                if (modifier.Value < 0)
                    continue;

                // modifier.Value is damage (positive), remainingHeal is healing amount (positive)
                if (modifier.Value <= remainingHeal)
                {
                    // Full modifier can be removed
                    remainingHeal -= modifier.Value;
                    _trauma.TryRemoveOrganDamageModifier(organ.Owner,
                        modifier.Key.Item2,
                        modifier.Key.Item1,
                        organ.Comp);
                    
                    if (remainingHeal <= 0)
                        break;
                }
                else
                {
                    // Partial modifier reduction: reduce by remainingHeal amount
                    // Since changeAmount is negative, we pass -remainingHeal
                    _trauma.TryChangeOrganDamageModifier(
                        organ.Owner,
                        -remainingHeal,
                        modifier.Key.Item2,
                        modifier.Key.Item1,
                        organ.Comp);
                    break;
                }
            }
        }
    }

    private bool CheckForTargetedComponents(EntityUid target, AdjustTraumas effect)
    {
        if (effect.TargetComponents == null)
            return true;

        effect.TargetComponentsLoaded ??= StringsToRegs(effect.TargetComponents);

        if (effect.MustHaveAllComponents)
        {
            var passedComps = effect.TargetComponentsLoaded.Count(x => EntityManager.HasComponent(target, x));

            if (passedComps == effect.TargetComponents.Length)
                return true;
        }
        else
        {
            foreach (var component in effect.TargetComponentsLoaded)
            {
                try
                {
                    if (EntityManager.HasComponent(target, component))
                    {
                        return true;
                    }
                }
                catch (KeyNotFoundException err)
                {
                    Log.Error($"Ошибка компонент не найден! {component.Name}");
                    continue;
                }
            }
        }

        return false;
    }

    private List<ComponentRegistration> StringsToRegs(string[]? input)
    {
        if (input == null || input.Length == 0)
            return [];

        var list = new List<ComponentRegistration>(input.Length);
        foreach (var name in input)
        {
            if (Factory.TryGetRegistration(name, out var registration))
                list.Add(registration);
            else if (Factory.GetComponentAvailability(name) != ComponentAvailability.Ignore)
                Log.Error($"{nameof(StringsToRegs)} failed: Unknown component name {name} passed to AdjustTraumasEntityEffectSystem!");
        }

        return list;
    }
}

[UsedImplicitly]
public sealed partial class AdjustTraumas : EntityEffectBase<AdjustTraumas>
{
    [DataField(required: true)]
    public FixedPoint2 Amount;

    [DataField(required: true)]
    public TraumaType TraumaType;

    [DataField("identifier")]
    public string ModifierIdentifier = "TraumaModifier";

    [DataField] public bool TargetBodyParts = true;
    [DataField] public bool TargetOrgans = true;

    [DataField(customTypeSerializer: typeof(CustomArraySerializer<string, ComponentNameSerializer>))]
    public string[]? TargetComponents;

    [NonSerialized]
    public List<ComponentRegistration>? TargetComponentsLoaded;

    [DataField]
    public bool MustHaveAllComponents;

    public override string? EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
    {
        var traumaTypeName = TraumaType switch
        {
            TraumaType.BoneDamage => "trauma-type-bone-damage",
            TraumaType.OrganDamage => "trauma-type-organ-damage",
            TraumaType.VeinsDamage => "trauma-type-veins-damage",
            TraumaType.NerveDamage => "trauma-type-nerve-damage",
            TraumaType.Dismemberment => "trauma-type-dismemberment",
            _ => "trauma-type-unknown"
        };

        var targetPaths = "";
        if (TargetComponents != null && TargetComponents.Length > 0)
        {
            var componentNames = string.Join(", ", TargetComponents);
            targetPaths = Loc.GetString(
                "entity-effect-guidebook-adjust-traumas-target-components",
                ("components", componentNames),
                ("mustHaveAll", MustHaveAllComponents));
        }

        var targetInfo = "";
        var targetParts = new List<string>();
        if (TargetBodyParts)
            targetParts.Add(Loc.GetString("entity-effect-guidebook-adjust-traumas-target-body-parts"));
        if (TargetOrgans)
            targetParts.Add(Loc.GetString("entity-effect-guidebook-adjust-traumas-target-organs"));
        
        if (targetParts.Count > 0)
        {
            targetInfo = Loc.GetString(
                "entity-effect-guidebook-adjust-traumas-targets",
                ("targets", string.Join(", ", targetParts)));
        }

        var mainText = Loc.GetString(
            "entity-effect-guidebook-adjust-traumas",
            ("chance", Probability),
            ("deltasign", MathF.Sign(Amount.Float())),
            ("amount", MathF.Abs(Amount.Float())),
            ("traumaType", Loc.GetString(traumaTypeName)));

        var result = mainText;
        if (!string.IsNullOrEmpty(targetInfo))
            result += " " + targetInfo;
        if (!string.IsNullOrEmpty(targetPaths))
            result += " " + targetPaths;

        return result;
    }
}
