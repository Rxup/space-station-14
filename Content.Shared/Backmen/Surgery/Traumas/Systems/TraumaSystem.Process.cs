using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Shared.Armor;
using Content.Shared.Backmen.CCVar;
using Content.Shared.Backmen.Surgery.Pain;
using Content.Shared.Backmen.Surgery.Pain.Components;
using Content.Shared.Backmen.Surgery.Traumas.Components;
using Content.Shared.Backmen.Surgery.Wounds;
using Content.Shared.Backmen.Surgery.Wounds.Components;
using Content.Shared.Body.Components;
using Content.Shared.Body.Part;
using Content.Shared.FixedPoint;
using Content.Shared.Inventory;
using JetBrains.Annotations;
using Robust.Shared.Containers;
using Robust.Shared.GameStates;
using Robust.Shared.Random;
using Robust.Shared.Utility;

namespace Content.Shared.Backmen.Surgery.Traumas.Systems;

public partial class TraumaSystem
{
    private const string TraumaContainerId = "Traumas";
    public static readonly TraumaType[] TraumasBlockingHealing = { TraumaType.BoneDamage, TraumaType.OrganDamage, TraumaType.Dismemberment };

    private float _nerveDamageThreshold = 0.7f;

    private void InitProcess()
    {
        SubscribeLocalEvent<TraumaInflicterComponent, ComponentStartup>(OnTraumaInflicterStartup);

        SubscribeLocalEvent<TraumaComponent, ComponentGetState>(OnComponentGet);
        SubscribeLocalEvent<TraumaComponent, ComponentHandleState>(OnComponentHandleState);

        Subs.CVar(_cfg, CCVars.NerveDamageThreshold, value => _nerveDamageThreshold = value, true);
    }

    private void OnComponentHandleState(EntityUid uid, TraumaComponent component, ref ComponentHandleState args)
    {
        if (args.Current is not TraumaComponentState state)
            return;

        component.TraumaTarget = TryGetEntity(state.TraumaTarget, out var e) ? e.Value : EntityUid.Invalid;
        component.HoldingWoundable = TryGetEntity(state.HoldingWoundable, out var e1) ? e1.Value : EntityUid.Invalid;

        component.TraumaType = state.TraumaType;
        component.TraumaSeverity = state.TraumaSeverity;
    }

    private void OnComponentGet(EntityUid uid, TraumaComponent comp, ref ComponentGetState args)
    {
        var state = new TraumaComponentState
        {
            TraumaTarget = TryGetNetEntity(comp.TraumaTarget, out var ne) ? ne.Value : NetEntity.Invalid,
            HoldingWoundable = TryGetNetEntity(comp.HoldingWoundable, out var ne1) ? ne1.Value : NetEntity.Invalid,

            TraumaType = comp.TraumaType,
            TraumaSeverity = comp.TraumaSeverity,
        };

        args.State = state;
    }

    private void OnTraumaInflicterStartup(
        Entity<TraumaInflicterComponent> woundEnt,
        ref ComponentStartup args)
    {
        woundEnt.Comp.TraumaContainer = Container.EnsureContainer<Container>(woundEnt, TraumaContainerId);
    }

    #region Public API

    public virtual EntityUid AddTrauma(
        EntityUid target,
        Entity<WoundableComponent> holdingWoundable,
        Entity<TraumaInflicterComponent> inflicter,
        TraumaType traumaType,
        FixedPoint2 severity)
    {
        // Server-only execution
        return EntityUid.Invalid;
    }

    [PublicAPI]
    public virtual void RemoveTrauma(Entity<TraumaComponent> trauma)
    {
        // Server-only execution
    }

    [PublicAPI]
    public virtual void RemoveTrauma(
        Entity<TraumaComponent> trauma,
        Entity<TraumaInflicterComponent> inflicterWound)
    {
        // Server-only execution
    }

    [PublicAPI]
    public IEnumerable<Entity<TraumaComponent>> GetAllWoundTraumas(
        EntityUid woundInflicter,
        TraumaInflicterComponent? component = null)
    {
        if (!Resolve(woundInflicter, ref component))
            yield break;

        foreach (var trauma in component.TraumaContainer.ContainedEntities)
        {
            yield return (trauma, Comp<TraumaComponent>(trauma));
        }
    }

    [PublicAPI]
    public bool HasAssociatedTrauma(
        EntityUid woundInflicter,
        TraumaType? traumaType = null,
        TraumaInflicterComponent? component = null)
    {
        if (!Resolve(woundInflicter, ref component))
            return false;

        foreach (var trauma in GetAllWoundTraumas(woundInflicter, component))
        {
            if (trauma.Comp.TraumaTarget == null)
                continue;

            if (trauma.Comp.TraumaType != traumaType && traumaType != null)
                continue;

            return true;
        }

        return false;
    }

    [PublicAPI]
    public bool TryGetAssociatedTrauma(
        EntityUid woundInflicter,
        [NotNullWhen(true)] out List<Entity<TraumaComponent>>? traumas,
        TraumaType? traumaType = null,
        TraumaInflicterComponent? component = null)
    {
        traumas = null;
        if (!Resolve(woundInflicter, ref component))
            return false;

        traumas = new List<Entity<TraumaComponent>>();
        foreach (var trauma in GetAllWoundTraumas(woundInflicter, component))
        {
            if (trauma.Comp.TraumaTarget == null)
                continue;

            if (trauma.Comp.TraumaType != traumaType && traumaType != null)
                continue;

            traumas.Add(trauma);
        }

        return true;
    }

    [PublicAPI]
    public bool HasWoundableTrauma(
        EntityUid woundable,
        TraumaType? traumaType = null,
        WoundableComponent? woundableComp = null)
    {
        if (!Resolve(woundable, ref woundableComp))
            return false;

        foreach (var woundEnt in Wound.GetWoundableWounds(woundable, woundableComp))
        {
            if (!TryComp<TraumaInflicterComponent>(woundEnt, out var inflicterComp))
                continue;

            if (HasAssociatedTrauma(woundEnt, traumaType, inflicterComp))
                return true;
        }

        return false;
    }

    [PublicAPI]
    public bool TryGetWoundableTrauma(
        EntityUid woundable,
        [NotNullWhen(true)] out List<Entity<TraumaComponent>>? traumas,
        TraumaType? traumaType = null,
        WoundableComponent? woundableComp = null)
    {
        traumas = null;
        if (!Resolve(woundable, ref woundableComp))
            return false;

        traumas = new List<Entity<TraumaComponent>>();
        foreach (var woundEnt in Wound.GetWoundableWounds(woundable, woundableComp))
        {
            if (!TryComp<TraumaInflicterComponent>(woundEnt, out var inflicterComp))
                continue;

            if (TryGetAssociatedTrauma(woundEnt, out var traumasFound, traumaType, inflicterComp))
                traumas.AddRange(traumasFound);
        }

        return traumas.Count > 0;
    }

    [PublicAPI]
    public bool HasBodyTrauma(
        EntityUid body,
        TraumaType? traumaType = null,
        BodyComponent? bodyComp = null)
    {
        return Resolve(body, ref bodyComp) && Body.GetBodyChildren(body, bodyComp).Any(bodyPart => HasWoundableTrauma(bodyPart.Id, traumaType));
    }

    [PublicAPI]
    public bool TryGetBodyTraumas(
        EntityUid body,
        [NotNullWhen(true)] out List<Entity<TraumaComponent>>? traumas,
        TraumaType? traumaType = null,
        BodyComponent? bodyComp = null)
    {
        traumas = null;
        if (!Resolve(body, ref bodyComp))
            return false;

        traumas = new List<Entity<TraumaComponent>>();
        foreach (var bodyPart in Body.GetBodyChildren(body, bodyComp))
        {
            if (TryGetWoundableTrauma(bodyPart.Id, out var traumasFound, traumaType))
                traumas.AddRange(traumasFound);
        }

        return traumas.Count > 0;
    }

    [PublicAPI]
    public List<TraumaType> RandomTraumaChance(
        EntityUid target,
        Entity<TraumaInflicterComponent> woundInflicter,
        FixedPoint2 severity,
        WoundableComponent? woundable = null)
    {
        var traumaList = new List<TraumaType>();
        if (!Resolve(target, ref woundable))
            return traumaList;

        if (woundInflicter.Comp.AllowedTraumas.Contains(TraumaType.BoneDamage) &&
            RandomBoneTraumaChance((target, woundable), woundInflicter))
            traumaList.Add(TraumaType.BoneDamage);

        if (woundInflicter.Comp.AllowedTraumas.Contains(TraumaType.NerveDamage) &&
            RandomNerveDamageChance((target, woundable), woundInflicter))
            traumaList.Add(TraumaType.NerveDamage);

        if (woundInflicter.Comp.AllowedTraumas.Contains(TraumaType.OrganDamage) &&
            RandomOrganTraumaChance((target, woundable), woundInflicter))
            traumaList.Add(TraumaType.OrganDamage);

        //if (RandomVeinsTraumaChance(woundable))
        //    traumaList.Add(TraumaType.VeinsDamage);

        if (woundInflicter.Comp.AllowedTraumas.Contains(TraumaType.Dismemberment) &&
            RandomDismembermentTraumaChance((target, woundable), woundInflicter))
            traumaList.Add(TraumaType.Dismemberment);

        return traumaList;
    }

    [PublicAPI]
    public FixedPoint2 GetArmourChanceDeduction(EntityUid body, Entity<TraumaInflicterComponent> inflicter, TraumaType traumaType, BodyPartType coverage)
    {
        var deduction = FixedPoint2.Zero;
        foreach (var ent in _inventory.GetHandOrInventoryEntities(body, SlotFlags.WITHOUT_POCKET))
        {
            if (!TryComp<ArmorComponent>(ent, out var armour))
                continue;

            if (!inflicter.Comp.AllowArmourDeduction.Contains(traumaType) && armour.TraumaDeductions[traumaType] >= 0)
                continue;

            if (armour.ArmorCoverage.Contains(coverage))
            {
                deduction += armour.TraumaDeductions[traumaType];
            }
        }

        return deduction;
    }

    [PublicAPI]
    public FixedPoint2 GetTraumaChanceDeduction(
        Entity<TraumaInflicterComponent> inflicter,
        EntityUid body,
        EntityUid traumaTarget,
        FixedPoint2 severity,
        TraumaType traumaType,
        BodyPartType coverage)
    {
        var deduction = FixedPoint2.Zero;
        deduction += GetArmourChanceDeduction(body, inflicter, traumaType, coverage);

        var traumaDeductionEvent = new TraumaChanceDeductionEvent(severity, traumaType, 0);
        RaiseLocalEvent(traumaTarget, ref traumaDeductionEvent);

        deduction += traumaDeductionEvent.ChanceDeduction;

        return deduction;
    }

    #endregion

    #region Trauma Chance Randoming

    [PublicAPI]
    public bool RandomBoneTraumaChance(Entity<WoundableComponent> target, Entity<TraumaInflicterComponent> woundInflicter)
    {
        var bodyPart = Comp<BodyPartComponent>(target);
        if (!bodyPart.Body.HasValue)
            return false; // Can't sever if already severed

        var bone = target.Comp.Bone.ContainedEntities.FirstOrNull();
        if (bone == null || !TryComp<BoneComponent>(bone, out var boneComp))
            return false;

        if (boneComp.BoneSeverity == BoneSeverity.Broken)
            return false;

        var deduction = GetTraumaChanceDeduction(
            woundInflicter,
            bodyPart.Body.Value,
            target,
            Comp<WoundComponent>(woundInflicter).WoundSeverityPoint,
            TraumaType.BoneDamage,
            bodyPart.PartType);

        // We do complete random to get the chance for trauma to happen,
        // We combine multiple parameters and do some math, to get the chance.
        // Even if we get 0.1 damage there's still a chance for injury to be applied, but with the extremely low chance.
        // The more damage, the bigger is the chance.
        var chance = FixedPoint2.Clamp(
            target.Comp.IntegrityCap / (target.Comp.WoundableIntegrity + boneComp.BoneIntegrity)
             * _boneTraumaChanceMultipliers[target.Comp.WoundableSeverity]
             - deduction + woundInflicter.Comp.TraumasChances[TraumaType.BoneDamage],
            0,
            1);

        return Random.Prob((float) chance);
    }

    [PublicAPI]
    public bool RandomNerveDamageChance(
        Entity<WoundableComponent> target,
        Entity<TraumaInflicterComponent> woundInflicter)
    {
        var bodyPart = Comp<BodyPartComponent>(target);
        if (!bodyPart.Body.HasValue)
            return false; // No entity to apply pain to

        if (!TryComp<NerveComponent>(target, out var nerve))
            return false;

        if (nerve.PainFeels < _nerveDamageThreshold)
            return false;

        var deduction = GetTraumaChanceDeduction(
            woundInflicter,
            bodyPart.Body.Value,
            target,
            Comp<WoundComponent>(woundInflicter).WoundSeverityPoint,
            TraumaType.NerveDamage,
            bodyPart.PartType);

        // literally dismemberment chance, but lower by default
        var chance =
            FixedPoint2.Clamp(
                target.Comp.WoundableIntegrity / target.Comp.IntegrityCap / 20
                - deduction + woundInflicter.Comp.TraumasChances[TraumaType.NerveDamage],
                0,
                1);

        return Random.Prob((float) chance);
    }

    [PublicAPI]
    public bool RandomOrganTraumaChance(
        Entity<WoundableComponent> target,
        Entity<TraumaInflicterComponent> woundInflicter)
    {
        var bodyPart = Comp<BodyPartComponent>(target);
        if (!bodyPart.Body.HasValue)
            return false; // No entity to apply pain to

        var totalIntegrity =
            Body.GetPartOrgans(target, bodyPart)
                .Aggregate(FixedPoint2.Zero, (current, organ) => current + organ.Component.OrganIntegrity);

        if (totalIntegrity <= 0) // No surviving organs
            return false;

        var deduction = GetTraumaChanceDeduction(
            woundInflicter,
            bodyPart.Body.Value,
            target,
            Comp<WoundComponent>(woundInflicter).WoundSeverityPoint,
            TraumaType.OrganDamage,
            bodyPart.PartType);

        if (target.Comp.WoundableIntegrity <= 0)
            return false;

        // organ damage is like, very deadly, but not yet
        // so like, like, yeah, we don't want a disabler to induce some EVIL ASS organ damage with a 0,000001% chance and ruin your round
        // Very unlikely to happen if your woundables are in a good condition
        var chance =
            FixedPoint2.Clamp(
                target.Comp.IntegrityCap / target.Comp.WoundableIntegrity / totalIntegrity
                - deduction + woundInflicter.Comp.TraumasChances[TraumaType.OrganDamage],
                0,
                1);

        return Random.Prob((float) chance);
    }

    [PublicAPI]
    public bool RandomDismembermentTraumaChance(
        Entity<WoundableComponent> target,
        Entity<TraumaInflicterComponent> woundInflicter)
    {
        var bodyPart = Comp<BodyPartComponent>(target);
        if (!bodyPart.Body.HasValue)
            return false; // Can't sever if already severed

        var parentWoundable = target.Comp.ParentWoundable;
        if (!parentWoundable.HasValue)
            return false;

        if (bodyPart.PartType == BodyPartType.Groin && Comp<WoundableComponent>(parentWoundable.Value).WoundableSeverity != WoundableSeverity.Critical)
            return false;

        var deduction = GetTraumaChanceDeduction(
            woundInflicter,
            bodyPart.Body.Value,
            target,
            Comp<WoundComponent>(woundInflicter).WoundSeverityPoint,
            TraumaType.Dismemberment,
            bodyPart.PartType);

        var bonePenalty = FixedPoint2.New(0.1);

        // Broken bones increase the chance of your limb getting delimbed
        var bone = target.Comp.Bone.ContainedEntities.FirstOrNull();
        if (bone != null && TryComp<BoneComponent>(bone, out var boneComp))
        {
            if (boneComp.BoneSeverity != BoneSeverity.Broken)
            {
                bonePenalty = boneComp.BoneIntegrity / boneComp.IntegrityCap;
            }
        }

        // random-y but not so random-y like bones. Heavily depends on woundable state and damage
        var chance =
            FixedPoint2.Clamp(
                1 - target.Comp.WoundableIntegrity / target.Comp.IntegrityCap * bonePenalty
                - deduction + woundInflicter.Comp.TraumasChances[TraumaType.Dismemberment],
                0,
                1);
        // getting hit again increases the chance

        return Random.Prob((float) chance);
    }

    #endregion
}
