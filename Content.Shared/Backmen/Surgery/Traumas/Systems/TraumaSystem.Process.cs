using System.Linq;
using Content.Shared.Armor;
using Content.Shared.Backmen.Surgery.Pain;
using Content.Shared.Backmen.Surgery.Traumas.Components;
using Content.Shared.Backmen.Surgery.Wounds;
using Content.Shared.Backmen.Surgery.Wounds.Components;
using Content.Shared.Body.Part;
using Content.Shared.FixedPoint;
using Content.Shared.Inventory;
using Robust.Shared.Random;

namespace Content.Shared.Backmen.Surgery.Traumas.Systems;

public partial class TraumaSystem
{
    private void InitProcess()
    {
        SubscribeLocalEvent<TraumaInflicterComponent, WoundSeverityPointChangedEvent>(OnWoundSeverityPointChanged);
    }

    private void OnWoundSeverityPointChanged(
        Entity<TraumaInflicterComponent> woundEnt,
        ref WoundSeverityPointChangedEvent args)
    {
        var traumasToInduce = RandomTraumaChance(args.Component.HoldingWoundable, woundEnt, args.NewSeverity);
        if (traumasToInduce.Count <= 0)
            return;

        var woundable = args.Component.HoldingWoundable;
        var woundableComp = Comp<WoundableComponent>(args.Component.HoldingWoundable);
        ApplyTraumas((woundable, woundableComp), woundEnt, traumasToInduce, args.NewSeverity);
    }

    #region Public API

    public List<TraumaType> RandomTraumaChance(
        EntityUid target,
        Entity<TraumaInflicterComponent> woundInflicter,
        FixedPoint2 severity,
        WoundableComponent? woundable = null)
    {
        var traumaList = new List<TraumaType>();
        if (!Resolve(target, ref woundable))
            return traumaList;

        if (woundInflicter.Comp.SeverityThreshold > severity)
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

    public FixedPoint2 GetArmourChanceDeduction(EntityUid body, Entity<TraumaInflicterComponent> inflicter, TraumaType traumaType, BodyPartType coverage)
    {
        var deduction = (FixedPoint2) 0;

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

    public FixedPoint2 GetTraumaChanceDeduction(
        Entity<TraumaInflicterComponent> inflicter,
        EntityUid body,
        EntityUid traumaTarget,
        FixedPoint2 severity,
        TraumaType traumaType,
        BodyPartType coverage)
    {
        var deduction = (FixedPoint2) 0;
        deduction += GetArmourChanceDeduction(body, inflicter, traumaType, coverage);

        var traumaDeductionEvent = new TraumaChanceDeductionEvent(severity, traumaType, 0);
        RaiseLocalEvent(traumaTarget, ref traumaDeductionEvent);

        deduction += traumaDeductionEvent.ChanceDeduction;

        return deduction;
    }

    #endregion

    #region Trauma Chance Randoming

    public bool RandomBoneTraumaChance(Entity<WoundableComponent> target, Entity<TraumaInflicterComponent> woundInflicter)
    {
        var bodyPart = Comp<BodyPartComponent>(target);
        if (!bodyPart.Body.HasValue)
            return false; // Can't sever if already severed

        var bone = Comp<BoneComponent>(target.Comp.Bone!.ContainedEntities[0]);

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
            target.Comp.IntegrityCap / (target.Comp.WoundableIntegrity + bone.BoneIntegrity)
             * _boneTraumaChanceMultipliers[target.Comp.WoundableSeverity]
             - deduction + woundInflicter.Comp.TraumasChances[TraumaType.BoneDamage],
            0,
            1);

        // Some examples of how this works:
        // 81 / (81 + 20) * 0.1 (Moderate) = 0.08. Or 8%:
        // 57 / (57 + 12) * 0.5 (Severe) = 0.41~. Or 41%;
        // 57 / (57 + 0) * 0.5 (Severe) = 0.5. Or 50%;
        // Yeah lol having your bone already messed up makes the chance of it damaging again higher

        return _random.Prob((float) chance);
    }

    public bool RandomNerveDamageChance(
        Entity<WoundableComponent> target,
        Entity<TraumaInflicterComponent> woundInflicter)
    {
        var bodyPart = Comp<BodyPartComponent>(target);
        if (!bodyPart.Body.HasValue)
            return false; // No entity to apply pain to

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
                target.Comp.WoundableIntegrity / target.Comp.IntegrityCap
                - deduction + woundInflicter.Comp.TraumasChances[TraumaType.NerveDamage],
                0,
                1);

        return _random.Prob((float) chance);
    }

    public bool RandomOrganTraumaChance(
        Entity<WoundableComponent> target,
        Entity<TraumaInflicterComponent> woundInflicter)
    {
        var bodyPart = Comp<BodyPartComponent>(target);
        if (!bodyPart.Body.HasValue)
            return false; // No entity to apply pain to

        var totalIntegrity =
            _body.GetPartOrgans(target, bodyPart)
                .Aggregate((FixedPoint2) 0, (current, organ) => current + organ.Component.OrganIntegrity);

        if (totalIntegrity <= 0) // No surviving organs
            return false;

        var deduction = GetTraumaChanceDeduction(
            woundInflicter,
            bodyPart.Body.Value,
            target,
            Comp<WoundComponent>(woundInflicter).WoundSeverityPoint,
            TraumaType.OrganDamage,
            bodyPart.PartType);

        // organ damage is like, very deadly, but not yet
        // so like, like, yeah, we don't want a disabler to induce some EVIL ASS organ damage with a 0,000001% chance and ruin your round
        // Very unlikely to happen if your woundables are in a good condition
        var chance =
            FixedPoint2.Clamp(
                target.Comp.IntegrityCap / target.Comp.WoundableIntegrity / totalIntegrity
                - deduction + woundInflicter.Comp.TraumasChances[TraumaType.OrganDamage],
                0,
                1);

        return _random.Prob((float) chance);
    }

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

        var parentComp = Comp<WoundableComponent>(parentWoundable.Value);
        if (parentComp.WoundableIntegrity == parentComp.IntegrityCap || target.Comp.WoundableIntegrity == target.Comp.IntegrityCap)
            return false; // just so you don't get split in half by a sneeze

        var deduction = GetTraumaChanceDeduction(
            woundInflicter,
            bodyPart.Body.Value,
            target,
            Comp<WoundComponent>(woundInflicter).WoundSeverityPoint,
            TraumaType.Dismemberment,
            bodyPart.PartType);

        // random-y but not so random-y like bones. Heavily depends on woundable state and damage
        var chance =
            FixedPoint2.Clamp(
                target.Comp.WoundableIntegrity / target.Comp.IntegrityCap
                - deduction + woundInflicter.Comp.TraumasChances[TraumaType.Dismemberment],
                0,
                1);
        // getting hit again increases the chance

        return _random.Prob((float) chance);
    }

    #endregion

    #region Private API

    private EntityUid AddTrauma(
        EntityUid target,
        Entity<WoundableComponent> holdingWoundable,
        Entity<TraumaInflicterComponent> inflicter,
        TraumaType traumaType,
        FixedPoint2 severity)
    {
        foreach (var trauma in inflicter.Comp.TraumaContainer.ContainedEntities)
        {
            var containedTraumaComp = Comp<TraumaComponent>(trauma);
            if (containedTraumaComp.TraumaType != traumaType || containedTraumaComp.TraumaTarget != target)
                continue;
            // Check for TraumaTarget isn't really necessary..
            // Right now wounds on a specified woundable can't wound other woundables, but in case IF something happens or IF someone decides to do that

            containedTraumaComp.TraumaSeverity = severity;
            return trauma;
        }

        var traumaEnt = Spawn(inflicter.Comp.TraumaPrototypes[traumaType]);
        var traumaComp = EnsureComp<TraumaComponent>(traumaEnt);

        traumaComp.TraumaSeverity = severity;

        traumaComp.TraumaTarget = target;
        traumaComp.HoldingWoundable = holdingWoundable;

        _container.Insert(traumaEnt, inflicter.Comp.TraumaContainer);

        // Raise the event on the woundable
        var ev = new TraumaInducedEvent((traumaEnt, traumaComp), target, severity, traumaType);
        RaiseLocalEvent(holdingWoundable, ref ev);

        // Raise the event on the inflicter (wound)
        var ev1 = new TraumaInducedEvent((traumaEnt, traumaComp), target, severity, traumaType);
        RaiseLocalEvent(inflicter, ref ev1);

        Dirty(traumaEnt, traumaComp);
        return traumaEnt;
    }

    private void ApplyTraumas(Entity<WoundableComponent> target, Entity<TraumaInflicterComponent> inflicter, List<TraumaType> traumas, FixedPoint2 severity)
    {
        var traumaList = traumas.ToList();
        if (traumaList.Contains(TraumaType.BoneDamage))
        {
            var targetBone = target.Comp.Bone!.ContainedEntities[0];

            var beforeBoneDamage = new BeforeTraumaInducedEvent(severity, targetBone, TraumaType.BoneDamage);
            RaiseLocalEvent(target, ref beforeBoneDamage);

            if (!beforeBoneDamage.Cancelled && ApplyBoneTrauma(targetBone, target, inflicter, severity))
            {
                var bodyPart = Comp<BodyPartComponent>(target);
                if (bodyPart.Body.HasValue && _consciousness.TryGetNerveSystem(bodyPart.Body.Value, out var nerveSys))
                {
                    _pain.TryAddPainModifier(
                        nerveSys.Value.Owner,
                        target.Owner,
                        "BoneDamageTrauma",
                        severity * 1.4f,
                        PainDamageTypes.TraumaticPain,
                        nerveSys.Value.Comp);
                }

                _sawmill.Debug($"A new trauma (Raw Severity: {severity}) was created on target: {target}. Type: Bone damage.");
            }
        }

        if (traumaList.Contains(TraumaType.NerveDamage))
        {
            var beforeNerveDamage = new BeforeTraumaInducedEvent(severity, target, TraumaType.NerveDamage);
            RaiseLocalEvent(target, ref beforeNerveDamage);

            var bodyPart = Comp<BodyPartComponent>(target);
            if (bodyPart.Body.HasValue && !beforeNerveDamage.Cancelled
                && _consciousness.TryGetNerveSystem(bodyPart.Body.Value, out var nerveSys))
            {
                var time = TimeSpan.FromSeconds((float) severity * 2.4);

                // Fooling people into thinking they have no pain.
                // 10 (raw pain) * 1.4 (multiplier) = 14 (actual pain)
                // 1 - 0.28 = 0.72 (the fraction of pain the person feels)
                // 14 * 0.72 = 10.08 (the pain the player can actually see) ... Barely noticeable :3
                _pain.TryAddPainMultiplier(nerveSys.Value,
                    "NerveDamage",
                    1.4f,
                    time: time);

                _pain.TryAddPainFeelsModifier(nerveSys.Value,
                    "NerveDamage",
                    target,
                    -0.28f,
                    time: time);

                foreach (var child in _wound.GetAllWoundableChildren(target))
                {
                    // Funner! Very unlucky of you if your torso gets hit. Rest in pieces
                    _pain.TryAddPainFeelsModifier(nerveSys.Value,
                        "NerveDamage",
                        child.Item1,
                        -0.7f,
                        time: time);
                }

                _sawmill.Debug( $"A new trauma (Caused by {severity} damage) was created on target: {target}. Type: NerveDamage.");
            }
        }

        // TODO: veins, would have been very lovely to integrate this into vascular system
        //if (RandomVeinsTraumaChance(woundable))
        //{
        //    traumaApplied = ApplyDamageToVeins(woundable.Veins!.ContainedEntities[0], severity * _veinsDamageMultipliers[woundable.WoundableSeverity]);
        //    _sawmill.Info(traumaApplied
        //        ? $"A new trauma (Raw Severity: {severity}) was created on target: {target} of type Vein damage"
        //        : $"Tried to create a trauma on target: {target}, but no trauma was applied. Type: Vein damage.");
        //}

        if (traumaList.Contains(TraumaType.OrganDamage))
        {
            var organs = _body.GetPartOrgans(target).ToList();
            _random.Shuffle(organs);

            var chosenOrgan = organs.FirstOrDefault();

            var beforeOrganDamage = new BeforeTraumaInducedEvent(severity, chosenOrgan.Id, TraumaType.OrganDamage);
            RaiseLocalEvent(target, ref beforeOrganDamage);

            if (!beforeOrganDamage.Cancelled)
            {
                if (!TryChangeOrganDamageModifier(chosenOrgan.Id, severity, target, "WoundableDamage"))
                {
                    TryCreateOrganDamageModifier(chosenOrgan.Id, severity, target, "WoundableDamage");
                    // Do not apply pain for organ damage is already supposed to be deadly; Unless it is NOT enough.
                }

                _sawmill.Debug( $"A new trauma (Caused by {severity} damage) was created on target: {target}; organ: {chosenOrgan}. Type: OrganDamage.");
            }
        }

        if (traumaList.Contains(TraumaType.Dismemberment))
        {
            var parentWoundable = target.Comp.ParentWoundable;
            if (parentWoundable.HasValue)
            {
                var beforeDismemberment = new BeforeTraumaInducedEvent(severity, parentWoundable.Value, TraumaType.Dismemberment);
                RaiseLocalEvent(target, ref beforeDismemberment);

                if (!_wound.IsWoundableRoot(target) && !beforeDismemberment.Cancelled)
                {
                    _wound.AmputateWoundable(parentWoundable.Value, target, target);
                    _sawmill.Debug($"A new trauma (Caused by {severity} damage) was created on target: {target}. Type: Dismemberment.");
                }
            }
        }
    }

    #endregion
}
