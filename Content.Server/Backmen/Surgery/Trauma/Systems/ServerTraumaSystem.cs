using System.Linq;
using Content.Shared.Backmen.Surgery.Pain;
using Content.Shared.Backmen.Surgery.Traumas;
using Content.Shared.Backmen.Surgery.Traumas.Components;
using Content.Shared.Backmen.Surgery.Traumas.Systems;
using Content.Shared.Backmen.Surgery.Wounds;
using Content.Shared.Backmen.Surgery.Wounds.Components;
using Content.Shared.Body.Organ;
using Content.Shared.Body.Part;
using Content.Shared.FixedPoint;
using JetBrains.Annotations;
using Robust.Shared.Utility;

namespace Content.Server.Backmen.Surgery.Trauma.Systems;

public sealed class ServerTraumaSystem : TraumaSystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TraumaInflicterComponent, WoundChangedEvent>(OnTraumaChanged);
        SubscribeLocalEvent<TraumaInflicterComponent, WoundHealAttemptEvent>(OnWoundHealAttempt);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var q = EntityQueryEnumerator<BoneComponent, MetaDataComponent>();
        while (q.MoveNext(out var uid, out var bone, out var meta))
        {
            if (Paused(uid, meta))
                continue;

            // Deleted or for some reason, or out of the woundable.
            if (!bone.BoneWoundable.HasValue || !WoundableQuery.TryComp(bone.BoneWoundable, out var woundable))
                continue;

            if (!BodyPartQuery.TryComp(bone.BoneWoundable, out var bodyPart) || !bodyPart.Body.HasValue)
                continue;

            if (woundable.WoundableSeverity is WoundableSeverity.Critical or WoundableSeverity.Loss)
                continue;

            if (bone.BoneSeverity is BoneSeverity.Broken or BoneSeverity.Damaged)
                continue;

            if (MobState.IsDead(bodyPart.Body.Value))
                continue;

            ApplyDamageToBone(uid, -bone.BoneRegenerationRate, bone);
        }

        // Organ regeneration
        var organQuery = EntityQueryEnumerator<OrganComponent, MetaDataComponent>();
        while (organQuery.MoveNext(out var uid, out var organ, out var meta))
        {
            if (Paused(uid, meta))
                continue;

            if (organ.Body == null)
                continue;

            if (MobState.IsDead(organ.Body.Value))
                continue;

            // Get the body part containing this organ
            if (organ.BodyPart == null || !BodyPartQuery.TryComp(organ.BodyPart, out var bodyPart))
                continue;

            if (!WoundableQuery.TryComp(organ.BodyPart, out var woundable))
                continue;

            if (woundable.WoundableSeverity is WoundableSeverity.Critical or WoundableSeverity.Loss)
                continue;

            // Skip if organ is already at full integrity
            if (organ.OrganIntegrity >= organ.IntegrityCap)
                continue;

            // Regenerate by reducing damage modifiers
            if (organ.IntegrityModifiers.Count == 0)
                continue;

            var regenerationAmount = organ.OrganRegenerationRate;
            var modifiersToProcess = organ.IntegrityModifiers.ToList();

            foreach (var modifier in modifiersToProcess)
            {
                if (modifier.Value <= 0)
                    continue;

                if (regenerationAmount <= 0)
                    break;

                var reduction = FixedPoint2.Min(regenerationAmount, modifier.Value);
                if (reduction <= 0)
                    continue;

                // Use TryChangeOrganDamageModifier to properly reduce the modifier
                // Negative change means healing
                if (TryChangeOrganDamageModifier(uid, -reduction, modifier.Key.Item2, modifier.Key.Item1, organ))
                {
                    regenerationAmount -= reduction;

                    // Check if modifier reached zero or below and remove it
                    if (organ.IntegrityModifiers.TryGetValue(modifier.Key, out var newValue) && newValue <= 0)
                    {
                        TryRemoveOrganDamageModifier(uid, modifier.Key.Item2, modifier.Key.Item1, organ);
                    }
                }
            }
        }
    }

    #region Very Private Server API

    private void OnTraumaChanged(
        Entity<TraumaInflicterComponent> wound,
        ref WoundChangedEvent args)
    {
        if (args.Delta < wound.Comp.SeverityThreshold)
            return;

        var traumasToInduce = RandomTraumaChance(args.Component.HoldingWoundable, wound, args.Delta);
        if (traumasToInduce.Count <= 0)
            return;

        var woundable = args.Component.HoldingWoundable;
        var woundableComp = Comp<WoundableComponent>(args.Component.HoldingWoundable);
        ApplyTraumas((woundable, woundableComp), wound, traumasToInduce, args.Delta);
    }

    private void OnWoundHealAttempt(
        Entity<TraumaInflicterComponent> inflicter,
        ref WoundHealAttemptEvent args)
    {
        var parentWoundable = args.Woundable;
        if (AnyTraumasBlockingHealing(parentWoundable, parentWoundable))
            args.Cancelled = true;
    }

    private void ApplyTraumas(Entity<WoundableComponent> target, Entity<TraumaInflicterComponent> inflicter, List<TraumaType> traumas, FixedPoint2 severity)
    {
        var bodyPart = Comp<BodyPartComponent>(target);
        if (!bodyPart.Body.HasValue)
            return;

        foreach (var trauma in traumas)
        {
            EntityUid? targetChosen = null;
            switch (trauma)
            {
                case TraumaType.BoneDamage:
                    targetChosen = target.Comp.Bone.ContainedEntities.FirstOrNull();
                    break;

                case TraumaType.OrganDamage:
                    var organs = Body.GetPartOrgans(target).ToList();
                    Random.Shuffle(organs);

                    var chosenOrgan = organs.FirstOrNull();
                    if (chosenOrgan != null)
                    {
                        targetChosen = chosenOrgan.Value.Id;
                    }

                    break;
                case TraumaType.Dismemberment:
                    targetChosen = target.Comp.ParentWoundable;
                    break;

                case TraumaType.NerveDamage:
                    targetChosen = target;
                    break;
            }

            if (targetChosen == null)
                continue;

            var beforeTraumaInduced = new BeforeTraumaInducedEvent(severity, targetChosen.Value, trauma);
            RaiseLocalEvent(target, ref beforeTraumaInduced);

            if (beforeTraumaInduced.Cancelled)
                continue;

            switch (trauma)
            {
                case TraumaType.BoneDamage:
                    if (!Consciousness.TryGetNerveSystem(bodyPart.Body.Value, out var nerveSysB))
                        break;

                    if (ApplyBoneTrauma(targetChosen.Value, target, inflicter, severity))
                    {
                        Pain.TryAddPainModifier(
                            nerveSysB.Value.Owner,
                                target.Owner,
                                "BoneDamage",
                                severity * 2,
                                PainType.TraumaticPain,
                                nerveSysB.Value.Comp);
                    }

                    break;

                case TraumaType.OrganDamage:
                    var traumaEnt = AddTrauma(targetChosen.Value, target, inflicter, TraumaType.OrganDamage, severity);
                    if (!TryChangeOrganDamageModifier(targetChosen.Value, severity, traumaEnt, "WoundableDamage"))
                    {
                        TryAddOrganDamageModifier(targetChosen.Value, severity, traumaEnt, "WoundableDamage");
                    }

                    break;

                case TraumaType.NerveDamage:
                    if (!Consciousness.TryGetNerveSystem(bodyPart.Body.Value, out var nerveSysN))
                        break;

                    var time = TimeSpan.FromSeconds((float) severity * 2.4);

                    // Fooling people into thinking they have no pain.
                    // 10 (raw pain) * 1.4 (multiplier) = 14 (actual pain)
                    // 1 - 0.28 = 0.72 (the fraction of pain the person feels)
                    // 14 * 0.72 = 10.08 (the pain the player can actually see) ... Barely noticeable :3
                    Pain.TryAddPainMultiplier(nerveSysN.Value,
                        "NerveDamage",
                        1.4f,
                        time: time);

                    Pain.TryAddPainFeelsModifier(nerveSysN.Value,
                        "NerveDamage",
                        target,
                        -0.28f,
                        time: time);

                    foreach (var child in Wound.GetAllWoundableChildren(target))
                    {
                        // Funner! Very unlucky of you if your torso gets hit. Rest in pieces
                        Pain.TryAddPainFeelsModifier(nerveSysN.Value,
                            "NerveDamage",
                            child,
                            -0.7f,
                            time: time);
                    }

                    break;

                case TraumaType.Dismemberment:
                    if (!Wound.IsWoundableRoot(target)
                        && Wound.TryInduceWound(targetChosen.Value, "Blunt", 10f, out var woundInduced))
                    {
                        AddTrauma(
                            targetChosen.Value,
                                (targetChosen.Value, Comp<WoundableComponent>(targetChosen.Value)),
                            (woundInduced.Value.Owner, EnsureComp<TraumaInflicterComponent>(woundInduced.Value.Owner)),
                            TraumaType.Dismemberment,
                            severity);

                        Wound.AmputateWoundable(targetChosen.Value, target, target);
                    }

                    break;
            }

            Log.Debug($"A new trauma (Raw Severity: {severity}) was created on target: {target}. Type: {trauma}.");
        }

        // TODO: veins, would have been very lovely to integrate this into vascular system
        //if (RandomVeinsTraumaChance(woundable))
        //{
        //    traumaApplied = ApplyDamageToVeins(woundable.Veins!.ContainedEntities[0], severity * _veinsDamageMultipliers[woundable.WoundableSeverity]);
        //    _sawmill.Info(traumaApplied
        //        ? $"A new trauma (Raw Severity: {severity}) was created on target: {target} of type Vein damage"
        //        : $"Tried to create a trauma on target: {target}, but no trauma was applied. Type: Vein damage.");
        //}
    }

    #endregion

    #region Public API

    [PublicAPI]
    public override EntityUid AddTrauma(
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

        Container.Insert(traumaEnt, inflicter.Comp.TraumaContainer);

        // Raise the event on the woundable
        var ev = new TraumaInducedEvent((traumaEnt, traumaComp), target, severity, traumaType);
        RaiseLocalEvent(holdingWoundable, ref ev);

        // Raise the event on the inflicter (wound)
        var ev1 = new TraumaInducedEvent((traumaEnt, traumaComp), target, severity, traumaType);
        RaiseLocalEvent(inflicter, ref ev1);

        Dirty(traumaEnt, traumaComp);
        return traumaEnt;
    }

    [PublicAPI]
    public override void RemoveTrauma(
        Entity<TraumaComponent> trauma,
        Entity<TraumaInflicterComponent> inflicterWound)
    {
        Container.Remove(trauma.Owner, inflicterWound.Comp.TraumaContainer, reparent: false, force: true);

        if (trauma.Comp.TraumaTarget != null)
        {
            var ev = new TraumaBeingRemovedEvent(trauma, trauma.Comp.TraumaTarget.Value, trauma.Comp.TraumaSeverity, trauma.Comp.TraumaType);
            RaiseLocalEvent(inflicterWound, ref ev);

            if (trauma.Comp.HoldingWoundable != null)
            {
                var ev1 = new TraumaBeingRemovedEvent(trauma, trauma.Comp.TraumaTarget.Value, trauma.Comp.TraumaSeverity, trauma.Comp.TraumaType);
                RaiseLocalEvent(trauma.Comp.HoldingWoundable.Value, ref ev1);
            }
        }

        QueueDel(trauma);
    }

    [PublicAPI]
    public override void RemoveTrauma(Entity<TraumaComponent> trauma)
    {
        if (!Container.TryGetContainingContainer((trauma.Owner, Transform(trauma.Owner), MetaData(trauma.Owner)), out var traumaContainer))
            return;

        if (!TryComp<TraumaInflicterComponent>(traumaContainer.Owner, out var traumaInflicter))
            return;

        RemoveTrauma(trauma, (traumaContainer.Owner, traumaInflicter));
    }

    #endregion

    #region Organs

    private void UpdateOrganIntegrity(EntityUid uid, OrganComponent organ)
    {
        var oldIntegrity = organ.OrganIntegrity;
        organ.OrganIntegrity = FixedPoint2.Clamp(organ.IntegrityModifiers
                .Aggregate(FixedPoint2.Zero, (current, modifier) => current + modifier.Value),
            0,
            organ.IntegrityCap);

        if (oldIntegrity != organ.OrganIntegrity)
        {
            var ev = new OrganIntegrityChangedEvent(oldIntegrity, organ.OrganIntegrity);
            RaiseLocalEvent(uid, ref ev);

            if (Container.TryGetContainingContainer((uid, Transform(uid), MetaData(uid)), out var container))
            {
                var ev1 = new OrganIntegrityChangedEventOnWoundable((uid, organ), oldIntegrity, organ.OrganIntegrity);
                RaiseLocalEvent(container.Owner, ref ev1);
            }
        }

        var nearestSeverity = organ.OrganSeverity;
        foreach (var (severity, value) in organ.IntegrityThresholds.OrderByDescending(kv => kv.Value))
        {
            if (organ.OrganIntegrity > value)
                continue;

            nearestSeverity = severity;
            break;
        }

        if (nearestSeverity != organ.OrganSeverity)
        {
            var ev = new OrganDamageSeverityChanged(organ.OrganSeverity, nearestSeverity);
            RaiseLocalEvent(uid, ref ev);

            if (Container.TryGetContainingContainer((uid, Transform(uid), MetaData(uid)), out var container))
            {
                var ev1 = new OrganDamageSeverityChangedOnWoundable((uid, organ), organ.OrganSeverity, nearestSeverity);
                RaiseLocalEvent(container.Owner, ref ev1);
            }
        }

        organ.OrganSeverity = nearestSeverity;
        Dirty(uid, organ);
    }

    [PublicAPI]
    public override bool TryRemoveOrganDamageModifier(
        EntityUid uid,
        EntityUid effectOwner,
        string identifier,
        OrganComponent? organ = null)
    {
        if (!OrganQuery.Resolve(uid, ref organ))
            return false;

        if (!organ.IntegrityModifiers.Remove((identifier, effectOwner)))
            return false;

        if (TryComp<TraumaComponent>(effectOwner, out var traumaComp))
        {
            RemoveTrauma((effectOwner, traumaComp));
        }

        UpdateOrganIntegrity(uid, organ);
        return true;
    }

    [PublicAPI]
    public override bool TryChangeOrganDamageModifier(
        EntityUid uid,
        FixedPoint2 change,
        EntityUid effectOwner,
        string identifier,
        OrganComponent? organ = null)
    {
        if (change == 0)
            return false;

        if (!OrganQuery.Resolve(uid, ref organ))
            return false;

        if (!organ.IntegrityModifiers.TryGetValue((identifier, effectOwner), out var value))
            return false;

        organ.IntegrityModifiers[(identifier, effectOwner)] = value + change;
        UpdateOrganIntegrity(uid, organ);

        return true;
    }

    [PublicAPI]
    public override bool TrySetOrganDamageModifier(
        EntityUid uid,
        FixedPoint2 severity,
        EntityUid effectOwner,
        string identifier,
        OrganComponent? organ = null)
    {
        if (severity == 0)
            return false;

        if (!OrganQuery.Resolve(uid, ref organ))
            return false;

        organ.IntegrityModifiers[(identifier, effectOwner)] = severity;
        UpdateOrganIntegrity(uid, organ);

        return true;
    }

    public override bool TryAddOrganDamageModifier(
        EntityUid uid,
        FixedPoint2 severity,
        EntityUid effectOwner,
        string identifier,
        OrganComponent? organ = null)
    {
        if (severity == 0)
            return false;

        if (!OrganQuery.Resolve(uid, ref organ))
            return false;

        if (!organ.IntegrityModifiers.TryAdd((identifier, effectOwner), severity))
            return false;
        UpdateOrganIntegrity(uid, organ);

        return true;
    }

    #endregion

    #region Bones

    private void CheckBoneSeverity(EntityUid bone, BoneComponent boneComp)
    {
        var nearestSeverity = boneComp.BoneSeverity;
        foreach (var (severity, value) in boneComp.BoneThresholds.OrderByDescending(kv => kv.Value))
        {
            if (boneComp.BoneIntegrity < value)
                continue;

            nearestSeverity = severity;
            break;
        }

        if (nearestSeverity != boneComp.BoneSeverity)
        {
            var ev = new BoneSeverityChangedEvent((bone, boneComp), boneComp.BoneSeverity, nearestSeverity);
            RaiseLocalEvent(bone, ref ev, true);
        }
        boneComp.BoneSeverity = nearestSeverity;

        Dirty(bone, boneComp);
    }



    [PublicAPI]
    public override bool ApplyBoneTrauma(
        EntityUid boneEnt,
        Entity<WoundableComponent> woundable,
        Entity<TraumaInflicterComponent> inflicter,
        FixedPoint2 inflicterSeverity,
        BoneComponent? boneComp = null)
    {
        if (!BoneQuery.Resolve(boneEnt, ref boneComp))
            return false;

        AddTrauma(boneEnt, woundable, inflicter, TraumaType.BoneDamage, inflicterSeverity);
        ApplyDamageToBone(boneEnt, inflicterSeverity, boneComp);

        return true;
    }

    [PublicAPI]
    public override bool SetBoneIntegrity(
        EntityUid bone,
        FixedPoint2 integrity,
        BoneComponent? boneComp = null)
    {
        if (!BoneQuery.Resolve(bone, ref boneComp))
            return false;

        var newIntegrity = FixedPoint2.Clamp(integrity, 0, boneComp.IntegrityCap);
        if (boneComp.BoneIntegrity == newIntegrity)
            return false;

        var ev = new BoneIntegrityChangedEvent((bone, boneComp), boneComp.BoneIntegrity, newIntegrity);
        RaiseLocalEvent(bone, ref ev);

        boneComp.BoneIntegrity = newIntegrity;
        CheckBoneSeverity(bone, boneComp);

        Dirty(bone, boneComp);
        return true;
    }

    public override bool ApplyDamageToBone(
        EntityUid bone,
        FixedPoint2 severity,
        BoneComponent? boneComp = null)
    {
        if (severity == 0)
            return false;

        if (!BoneQuery.Resolve(bone, ref boneComp))
            return false;

        var newIntegrity = FixedPoint2.Clamp(boneComp.BoneIntegrity - severity, 0, boneComp.IntegrityCap);
        if (boneComp.BoneIntegrity == newIntegrity)
            return false;

        var ev = new BoneIntegrityChangedEvent((bone, boneComp), boneComp.BoneIntegrity, newIntegrity);
        RaiseLocalEvent(bone, ref ev);

        boneComp.BoneIntegrity = newIntegrity;
        CheckBoneSeverity(bone, boneComp);

        Dirty(bone, boneComp);
        return true;
    }

    #endregion
}
