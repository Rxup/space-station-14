using System.Linq;
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
            foreach (var modifier in organ.Comp.IntegrityModifiers)
            {
                // Healing modifier are not getting removed
                if (modifier.Value < 0)
                    continue;

                if (modifier.Value > -changeAmount)
                {
                    _trauma.TryChangeOrganDamageModifier(
                        organ.Owner,
                        changeAmount,
                        modifier.Key.Item2,
                        modifier.Key.Item1,
                        organ.Comp);
                    break;
                }

                changeAmount += modifier.Value;
                _trauma.TryRemoveOrganDamageModifier(organ.Owner,
                    modifier.Key.Item2,
                    modifier.Key.Item1,
                    organ.Comp);
            }
        }
    }

    private bool CheckForTargetedComponents(EntityUid target, AdjustTraumas effect)
    {
        if (effect.TargetComponents == null)
            return true;

        if (effect.MustHaveAllComponents)
        {
            var passedComps =
                (from component in effect.TargetComponents
                    let compToPass = component.Value.GetType()
                    where EntityManager.HasComponent(target, compToPass)
                    select component.Value.Component).ToList();

            if (passedComps.Count == effect.TargetComponents.Count)
                return true;
        }
        else
        {
            foreach (var component in effect.TargetComponents
                         .Select(component => component.Value.GetType()))
            {
                try
                {
                    if (EntityManager.HasComponent(target, component))
                    {
                        return true;
                    }
                }
                catch (KeyNotFoundException)
                {
                    continue;
                }
            }
        }

        return false;
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

    [DataField]
    public ComponentRegistry? TargetComponents;

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

        return Loc.GetString(
            "entity-effect-guidebook-adjust-traumas",
            ("chance", Probability),
            ("deltasign", MathF.Sign(Amount.Float())),
            ("amount", MathF.Abs(Amount.Float())),
            ("traumaType", Loc.GetString(traumaTypeName)));
    }
}
