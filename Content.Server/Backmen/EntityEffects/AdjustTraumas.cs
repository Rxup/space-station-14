using System.Linq;
using Content.Server.Body.Systems;
using Content.Shared.Backmen.Surgery.Pain.Components;
using Content.Shared.Backmen.Surgery.Pain.Systems;
using Content.Shared.Backmen.Surgery.Traumas;
using Content.Shared.Backmen.Surgery.Traumas.Components;
using Content.Shared.Backmen.Surgery.Traumas.Systems;
using Content.Shared.Backmen.Surgery.Wounds.Components;
using Content.Shared.Body.Organ;
using Content.Shared.EntityEffects;
using Content.Shared.FixedPoint;
using JetBrains.Annotations;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Utility;

namespace Content.Server.Backmen.EntityEffects;

[UsedImplicitly]
public sealed partial class AdjustTraumas : EntityEffect
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

    protected override string? ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys) => null;

    private void ApplyTraumaEffects(
        EntityUid body,
        EntityUid target,
        FixedPoint2 changeAmount,
        IEntityManager entMan,
        BodySystem bodySys,
        TraumaSystem traumaSys)
    {
        if (entMan.TryGetComponent<OrganComponent>(target, out var organComp))
        {
            if (TraumaType != TraumaType.OrganDamage)
                return;

            if (!organComp.BodyPart.HasValue)
                return;

            ChangeOrganModifiers((target, organComp), changeAmount, traumaSys);
        }
        else
        {
            switch (TraumaType)
            {
                case TraumaType.BoneDamage:
                    var comp = entMan.GetComponent<WoundableComponent>(target);
                    var bone = comp.Bone.ContainedEntities.FirstOrNull();
                    if (bone == null || !entMan.TryGetComponent(bone, out BoneComponent? boneComp))
                        break;

                    traumaSys.ApplyDamageToBone(bone.Value, changeAmount, boneComp);
                    break;
                case TraumaType.OrganDamage:
                    var organs = bodySys.GetPartOrgans(target).ToList();
                    if (organs.Count == 0)
                        break;

                    if (changeAmount > 0)
                    {
                        var organTarget = IoCManager.Resolve<IRobustRandom>().PickAndTake(organs);
                        ChangeOrganModifiers(organTarget, changeAmount, traumaSys);
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
                            ChangeOrganModifiers(organTarget.Value, changeAmount, traumaSys);
                    }

                    break;
                case TraumaType.VeinsDamage:
                    // not implemented yet
                    break;
                case TraumaType.NerveDamage:
                    var painSystem = entMan.System<PainSystem>();
                    if (changeAmount > 0)
                    {
                        foreach (var bodyPart in bodySys.GetBodyChildren(body))
                        {
                            if (!entMan.TryGetComponent(bodyPart.Id, out NerveComponent? nerve))
                                continue;

                            // you actually have AdjustPainFeels for this, but fine
                            if (!painSystem.TryChangePainFeelsModifier(
                                    body,
                                    ModifierIdentifier,
                                    bodyPart.Id,
                                    changeAmount,
                                    nerve))
                            {
                                painSystem.TryAddPainFeelsModifier(body,
                                    ModifierIdentifier,
                                    bodyPart.Id,
                                    changeAmount,
                                    nerve);
                            }
                        }
                    }
                    else
                    {
                        var available = changeAmount;
                        foreach (var bodyPart in bodySys.GetBodyChildren(body))
                        {
                            if (!entMan.TryGetComponent(bodyPart.Id, out NerveComponent? nerve))
                                continue;

                            foreach (var painFeelsMod in nerve.PainFeelingModifiers)
                            {
                                var change = FixedPoint2.Abs(painFeelsMod.Value.Change);
                                if (change > available)
                                {
                                    var newChange = change - available;
                                    painSystem.TrySetPainFeelsModifier(
                                        painFeelsMod.Key.Item1,
                                        painFeelsMod.Key.Item2,
                                        bodyPart.Id,
                                        newChange,
                                        nerve: nerve);
                                    break;
                                }

                                available -= change;
                                painSystem.TryRemovePainFeelsModifier(
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

    private void ChangeOrganModifiers(Entity<OrganComponent> organ, FixedPoint2 changeAmount, TraumaSystem traumaSys)
    {
        if (changeAmount > 0)
        {
            if (!traumaSys.TryChangeOrganDamageModifier(
                    organ.Owner,
                    changeAmount,
                    organ.Owner,
                    ModifierIdentifier,
                    organ.Comp))
            {
                traumaSys.TryAddOrganDamageModifier(
                    organ.Owner,
                    changeAmount,
                    organ.Owner,
                    ModifierIdentifier,
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
                    traumaSys.TryChangeOrganDamageModifier(
                        organ.Owner,
                        changeAmount,
                        modifier.Key.Item2,
                        modifier.Key.Item1,
                        organ.Comp);
                    break;
                }

                changeAmount += modifier.Value;
                traumaSys.TryRemoveOrganDamageModifier(organ.Owner,
                    modifier.Key.Item2,
                    modifier.Key.Item1,
                    organ.Comp);
            }
        }
    }

    private bool CheckForTargetedComponents(EntityUid target, IEntityManager entMan)
    {
        if (TargetComponents == null)
            return true;

        if (MustHaveAllComponents)
        {
            var passedComps =
                (from component in TargetComponents
                    let compToPass = component.Value.GetType()
                    where entMan.HasComponent(target, compToPass)
                    select component.Value.Component).ToList();

            if (passedComps.Count == TargetComponents.Count)
                return true;
        }
        else
        {
            return
                TargetComponents
                    .Select(component => component.Value.GetType())
                    .Any(compToPass => entMan.HasComponent(target, compToPass));
        }

        return false;
    }

    public override void Effect(EntityEffectBaseArgs args)
    {
        var scale = FixedPoint2.New(1);

        if (args is EntityEffectReagentArgs reagentArgs)
        {
            scale = reagentArgs.Quantity * reagentArgs.Scale;
        }

        var bodySys = args.EntityManager.System<BodySystem>();
        var traumaSys = args.EntityManager.System<TraumaSystem>();

        var possibleTraumaTargets = new List<EntityUid>();

        if (TargetBodyParts)
        {
            possibleTraumaTargets
                .AddRange(from bodyPart in bodySys.GetBodyChildren(args.TargetEntity)
                    where args.EntityManager.HasComponent<WoundableComponent>(bodyPart.Id)
                    where CheckForTargetedComponents(bodyPart.Id, args.EntityManager)
                    select bodyPart.Id);
        }

        if (TargetOrgans)
        {
            possibleTraumaTargets.AddRange
                (from organ in bodySys.GetBodyOrgans(args.TargetEntity)
                    where CheckForTargetedComponents(organ.Id, args.EntityManager)
                    select organ.Id);
        }

        if (possibleTraumaTargets.Count == 0)
            return;

        var changeAmount = Amount * scale;
        var target = IoCManager.Resolve<IRobustRandom>().PickAndTake(possibleTraumaTargets);

        ApplyTraumaEffects(args.TargetEntity, target, changeAmount, args.EntityManager, bodySys, traumaSys);
    }
}
