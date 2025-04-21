using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Shared.Backmen.CCVar;
using Content.Shared.Backmen.Surgery.Traumas;
using Content.Shared.Backmen.Surgery.Traumas.Components;
using Content.Shared.Backmen.Surgery.Wounds;
using Content.Shared.Backmen.Surgery.Wounds.Components;
using Content.Shared.Backmen.Surgery.Wounds.Systems;
using Content.Shared.Backmen.Targeting;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Damage.Prototypes;
using Content.Shared.FixedPoint;
using Content.Shared.Humanoid;
using Content.Shared.Inventory;
using JetBrains.Annotations;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.Backmen.Surgery.Wounds.Systems;

public sealed class ServerWoundSystem : WoundSystem
{
    private float _medicalHealingTickrate = 0.5f;

    private float _woundScarChance;
    private float _woundTransferPart;

    private float _maxWoundSeverity;

    [ValidatePrototypeId<EntityPrototype>]
    private const string BluntWoundId = "Blunt";

    [Dependency] private readonly IPrototypeManager _prototype = default!;

    public override void Initialize()
    {
        base.Initialize();

        Subs.CVar(Cfg, CCVars.MedicalHealingTickrate, val => _medicalHealingTickrate = val, true);
        Subs.CVar(Cfg, CCVars.WoundScarChance, val => _woundScarChance = val, true);
        Subs.CVar(Cfg, CCVars.MaxWoundSeverity, val => _maxWoundSeverity = val, true);
        Subs.CVar(Cfg, CCVars.WoundTransferPart, val => _woundTransferPart = val, true);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var timeToHeal = 1 / _medicalHealingTickrate;
        using var query = EntityQueryEnumerator<WoundableComponent, MetaDataComponent>();
        while (query.MoveNext(out var ent, out var woundable, out var metaData))
        {
            if (Paused(ent, metaData))
                continue;

            woundable.HealingRateAccumulated += frameTime;
            if (woundable.HealingRateAccumulated < timeToHeal)
                continue;

            if (woundable.Wounds == null || woundable.Wounds.Count == 0)
                continue;

            woundable.HealingRateAccumulated -= timeToHeal;

            var woundsToHeal =
                GetWoundableWounds(ent, woundable).Where(wound => CanHealWound(wound, wound)).ToList();

            if (woundsToHeal.Count == 0)
                continue;

            var healAmount = -woundable.HealAbility / woundsToHeal.Count;

            Entity<WoundableComponent> owner = (ent, woundable);
            foreach (var x in woundsToHeal)
            {
                ApplyWoundSeverity(x,
                    ApplyHealingRateMultipliers((x,x), owner, healAmount, owner),
                    x);
            }
        }
    }

    #region Helpers, Wound Inducing

    [PublicAPI]
    public override bool TryInduceWounds(
        EntityUid uid,
        WoundSpecifier wounds,
        out List<Entity<WoundComponent>> woundsInduced,
        WoundableComponent? woundable = null)
    {
        woundsInduced = new List<Entity<WoundComponent>>();
        if (!WoundableQuery.Resolve(uid, ref woundable))
            return false;

        foreach (var woundToInduce in wounds.WoundDict)
        {
            if (!TryInduceWound(uid, woundToInduce.Key, woundToInduce.Value, out var woundInduced, woundable))
                return false;

            woundsInduced.Add(woundInduced.Value);
        }

        return true;
    }

    [PublicAPI]
    public override bool TryInduceWound(
        EntityUid uid,
        string woundId,
        FixedPoint2 severity,
        [NotNullWhen(true)] out Entity<WoundComponent>? woundInduced,
        WoundableComponent? woundable = null)
    {
        woundInduced = null;
        if (!WoundableQuery.Resolve(uid, ref woundable))
            return false;

        if (TryContinueWound(uid, woundId, severity, out woundInduced, woundable))
            return true;

        return TryCreateWound(
            uid,
            woundId,
            severity,
            out woundInduced,
            (from @group in _prototype.EnumeratePrototypes<DamageGroupPrototype>()
                where @group.DamageTypes.Contains(woundId)
                select @group).FirstOrDefault(),
            woundable);
    }

    [PublicAPI]
    public override bool TryCreateWound(
        EntityUid uid,
        string woundProtoId,
        FixedPoint2 severity,
        [NotNullWhen(true)] out Entity<WoundComponent>? woundCreated,
        DamageGroupPrototype? damageGroup,
        WoundableComponent? woundable = null)
    {
        woundCreated = null;
        if (!IsWoundPrototypeValid(woundProtoId))
            return false;

        if (!WoundableQuery.Resolve(uid, ref woundable))
            return false;

        var wound = Spawn(woundProtoId);
        if (AddWound(uid, wound, severity, damageGroup))
        {
            woundCreated = (wound, WoundQuery.Comp(wound));
        }
        else
        {
            // The wound failed some important checks, and we cannot let an invalid wound to be spawned!
            QueueDel(wound);
            return false;
        }

        return true;
    }

    [PublicAPI]
    public override bool TryContinueWound(
        EntityUid uid,
        string id,
        FixedPoint2 severity,
        [NotNullWhen(true)] out Entity<WoundComponent>? woundContinued,
        WoundableComponent? woundable = null)
    {
        woundContinued = null;
        if (!IsWoundPrototypeValid(id))
            return false;

        if (!WoundableQuery.Resolve(uid, ref woundable))
            return false;

        var proto = _prototype.Index(id);
        foreach (var wound in GetWoundableWounds(uid, woundable))
        {
            if (proto.ID != wound.Comp.DamageType)
                continue;

            ApplyWoundSeverity(wound, severity, wound);
            woundContinued = wound;

            return true;
        }

        return false;
    }

    [PublicAPI]
    public override bool TryMakeScar(
        EntityUid wound,
        [NotNullWhen(true)] out Entity<WoundComponent>? scarWound,
        WoundComponent? woundComponent = null)
    {
        scarWound = null;
        if (!WoundQuery.Resolve(wound, ref woundComponent))
            return false;

        if (!Random.Prob(_woundScarChance))
            return false;

        if (woundComponent.ScarWound == null || woundComponent.IsScar)
            return false;

        if (!TryCreateWound(woundComponent.HoldingWoundable,
                woundComponent.ScarWound,
                0.01f,
                out var createdWound,
                woundComponent.DamageGroup))
            return false;

        scarWound = createdWound;
        return true;
    }

    [PublicAPI]
    public override void ApplyWoundSeverity(
        EntityUid uid,
        FixedPoint2 severity,
        WoundComponent? wound = null)
    {
        if (!WoundQuery.Resolve(uid, ref wound))
            return;

        // No reason to update the wound if the woundable it is stored in is about to be deleted.
        if (TerminatingOrDeleted(wound.HoldingWoundable) || !WoundableQuery.TryComp(wound.HoldingWoundable, out var holdingComp))
            return;

        var old = wound.WoundSeverityPoint;
        wound.WoundSeverityPoint = severity > 0
            ? FixedPoint2.Clamp(old + ApplySeverityModifiers(wound.HoldingWoundable, severity, holdingComp), 0, _maxWoundSeverity)
            : FixedPoint2.Clamp(old + severity, 0, _maxWoundSeverity);

        if (wound.WoundSeverityPoint == old)
            return;

        Entity<WoundableComponent> holdingWoundable = (wound.HoldingWoundable, holdingComp);

        RaiseWoundEvents(uid, holdingWoundable, wound, old, holdingWoundable);
        CheckSeverityThresholds(uid, wound);

        UpdateWoundableIntegrity(holdingWoundable, holdingWoundable);
        CheckWoundableSeverityThresholds(holdingWoundable, holdingWoundable);
    }

    [PublicAPI]
    public override void SetWoundSeverity(
        EntityUid uid,
        FixedPoint2 severity,
        WoundComponent? wound = null)
    {
        if (!WoundQuery.Resolve(uid, ref wound))
            return;

        // No reason to update the wound if the woundable it is stored in is about to be deleted.
        if (TerminatingOrDeleted(wound.HoldingWoundable) || !WoundableQuery.TryComp(wound.HoldingWoundable, out var holdingComp))
            return;

        var old = wound.WoundSeverityPoint;
        wound.WoundSeverityPoint =
            FixedPoint2.Clamp(ApplySeverityModifiers(wound.HoldingWoundable, severity, holdingComp), 0, _maxWoundSeverity);

        if (wound.WoundSeverityPoint == old)
            return;

        Entity<WoundableComponent> holdingWoundable = (wound.HoldingWoundable, holdingComp);

        RaiseWoundEvents(uid, holdingWoundable, wound, old, holdingWoundable);
        CheckSeverityThresholds(uid, wound);

        UpdateWoundableIntegrity(holdingWoundable, holdingWoundable);
        CheckWoundableSeverityThresholds(holdingWoundable, holdingWoundable);
    }

    #endregion

    #region Severity Multipliers

    [PublicAPI]
    public override bool TryAddWoundableSeverityMultiplier(
        EntityUid uid,
        EntityUid owner,
        FixedPoint2 change,
        string identifier,
        WoundableComponent? component = null)
    {
        if (!WoundableQuery.Resolve(uid, ref component) || component.Wounds == null)
            return false;

        if (!component.SeverityMultipliers.TryAdd(owner, new WoundableSeverityMultiplier(change, identifier)))
            return false;

        foreach (var wound in GetWoundableWounds(uid, component))
        {
            CheckSeverityThresholds(wound, wound);
        }

        UpdateWoundableIntegrity(uid, component);
        CheckWoundableSeverityThresholds(uid, component);

        return true;
    }

    [PublicAPI]
    public override bool TryRemoveWoundableSeverityMultiplier(
        EntityUid uid,
        string identifier,
        WoundableComponent? component = null)
    {
        if (!WoundableQuery.Resolve(uid, ref component) || component.Wounds == null)
            return false;

        foreach (var multiplier in
                 component.SeverityMultipliers.Where(multiplier => multiplier.Value.Identifier == identifier))
        {
            if (!component.SeverityMultipliers.Remove(multiplier.Key, out _))
                return false;

            foreach (var wound in component.Wounds.ContainedEntities)
            {
                CheckSeverityThresholds(wound);
            }

            UpdateWoundableIntegrity(uid, component);
            CheckWoundableSeverityThresholds(uid, component);

            return true;
        }

        return false;
    }

    [PublicAPI]
    public override bool TryChangeWoundableSeverityMultiplier(
        EntityUid uid,
        string identifier,
        FixedPoint2 change,
        WoundableComponent? component = null)
    {
        if (!WoundableQuery.Resolve(uid, ref component) || component.Wounds == null)
            return false;

        foreach (var multiplier in
                 component.SeverityMultipliers.Where(multiplier => multiplier.Value.Identifier == identifier).ToList())
        {
            component.SeverityMultipliers.Remove(multiplier.Key, out var value);

            value.Change = change;
            component.SeverityMultipliers.Add(multiplier.Key, value);

            foreach (var wound in component.Wounds.ContainedEntities)
            {
                CheckSeverityThresholds(wound);
            }

            UpdateWoundableIntegrity(uid, component);
            CheckWoundableSeverityThresholds(uid, component);

            return true;
        }

        return false;
    }

    [PublicAPI]
    public override bool TryAddHealingRateMultiplier(
        EntityUid owner,
        EntityUid woundable,
        string identifier,
        FixedPoint2 change,
        WoundableComponent? component = null)
    {
        return Resolve(woundable, ref component) && component.HealingMultipliers.TryAdd(owner, new WoundableHealingMultiplier(change, identifier));
    }

    [PublicAPI]
    public override bool TryRemoveHealingRateMultiplier(
        EntityUid owner,
        EntityUid woundable,
        WoundableComponent? component = null)
    {
        return Resolve(woundable, ref component) && component.HealingMultipliers.Remove(owner);
    }

    #endregion

    #region Detaching Woundables

    [PublicAPI]
    public override void DestroyWoundable(
        EntityUid parentWoundableEntity,
        EntityUid woundableEntity,
        WoundableComponent? woundableComp = null,
        WoundableComponent? parentWoundableComp = null)
    {
        if (!WoundableQuery.Resolve(woundableEntity, ref woundableComp)
            || !WoundableQuery.Resolve(parentWoundableEntity, ref parentWoundableComp))
            return;

        var bodyPart = Comp<BodyPartComponent>(woundableEntity);
        if (bodyPart.Body == null)
        {
            DropWoundableOrgans(woundableEntity, woundableComp);
            QueueDel(woundableEntity);

            return;
        }

        var key = bodyPart.ToHumanoidLayers();
        if (key == null)
            return;

        // if wounds amount somehow changes it triggers an enumeration error. owch
        woundableComp.AllowWounds = false;
        woundableComp.WoundableSeverity = WoundableSeverity.Loss;

        if (TryComp<TargetingComponent>(bodyPart.Body.Value, out var targeting))
        {
            targeting.BodyStatus = GetWoundableStatesOnBodyPainFeels(bodyPart.Body.Value);
            Dirty(bodyPart.Body.Value, targeting);

            RaiseNetworkEvent(new TargetIntegrityChangeEvent(GetNetEntity(bodyPart.Body.Value)), bodyPart.Body.Value);
        }

        Audio.PlayPvs(woundableComp.WoundableDestroyedSound, bodyPart.Body.Value);

        if (IsWoundableRoot(woundableEntity, woundableComp))
        {
            DropWoundableOrgans(woundableEntity, woundableComp);
            DestroyWoundableChildren(woundableEntity, woundableComp);

            QueueDel(woundableEntity);
            Dirty(parentWoundableEntity, parentWoundableComp);

            Body.GibBody(bodyPart.Body.Value); // More blood for the Blood Gods!
        }
        else
        {
            if (!Containers.TryGetContainingContainer(parentWoundableEntity, woundableEntity, out var container))
                return;

            if (bodyPart.Body is not null)
            {
                if (TryComp<InventoryComponent>(bodyPart.Body, out var inventory) // Prevent error for non-humanoids
                    && Body.GetBodyPartCount(bodyPart.Body.Value, bodyPart.PartType) == 1
                    && Body.TryGetPartSlotContainerName(bodyPart.PartType, out var containerNames))
                {
                    foreach (var containerName in containerNames)
                    {
                        Inventory.DropSlotContents(bodyPart.Body.Value, containerName, inventory);
                    }
                }

                if (bodyPart.PartType is BodyPartType.Hand or BodyPartType.Arm)
                {
                    // Prevent anomalous behaviour
                    Hands.TryDrop(bodyPart.Body.Value, woundableEntity);
                }
            }

            DropWoundableOrgans(woundableEntity, woundableComp);
            DestroyWoundableChildren(woundableEntity, woundableComp);

            foreach (var wound in GetWoundableWounds(woundableEntity, woundableComp))
            {
                TransferWoundDamage(parentWoundableEntity, woundableEntity, wound, parentWoundableComp, wound);
            }

            if (TryInduceWound(parentWoundableEntity, BluntWoundId, 15f, out var woundEnt, parentWoundableComp))
            {
                Trauma.AddTrauma(
                    parentWoundableEntity,
                    (parentWoundableEntity, parentWoundableComp),
                    (woundEnt.Value.Owner, EnsureComp<TraumaInflicterComponent>(woundEnt.Value.Owner)),
                    TraumaType.Dismemberment,
                    15f);
            }

            foreach (var wound in
                     GetWoundableWoundsWithComp<BleedInflicterComponent>(parentWoundableEntity, parentWoundableComp))
            {
                // Bleeding :3
                wound.Comp2.ScalingLimit += 10;
            }

            var bodyPartId = container.ID;
            Body.DetachPart(parentWoundableEntity, SharedBodySystem.GetPartSlotContainerIdFromContainer(bodyPartId), woundableEntity);

            QueueDel(woundableEntity);
        }
    }

    [PublicAPI]
    public override void AmputateWoundable(
        EntityUid parentWoundableEntity,
        EntityUid woundableEntity,
        WoundableComponent? woundableComp = null,
        WoundableComponent? parentWoundableComp = null)
    {
        if (!WoundableQuery.Resolve(woundableEntity, ref woundableComp)
            || !WoundableQuery.Resolve(parentWoundableEntity, ref parentWoundableComp))
            return;

        var bodyPart = Comp<BodyPartComponent>(parentWoundableEntity);
        if (!bodyPart.Body.HasValue)
            return;

        Audio.PlayPvs(woundableComp.WoundableDelimbedSound, bodyPart.Body.Value);

        foreach (var wound in GetWoundableWounds(woundableEntity, woundableComp))
        {
            TransferWoundDamage(parentWoundableEntity, woundableEntity, wound);
        }

        foreach (var wound in
                 GetWoundableWoundsWithComp<BleedInflicterComponent>(parentWoundableEntity, parentWoundableComp))
        {
            wound.Comp2.ScalingLimit += 6;
        }

        AmputateWoundableSafely(parentWoundableEntity, woundableEntity, woundableComp, parentWoundableComp);
        Throwing.TryThrow(woundableEntity, Random.NextAngle().ToWorldVec() * 7f, Random.Next(8, 24));

        Dirty(woundableEntity, woundableComp);
    }

    [PublicAPI]
    public override void AmputateWoundableSafely(
        EntityUid parentWoundableEntity,
        EntityUid woundableEntity,
        WoundableComponent? woundableComp = null,
        WoundableComponent? parentWoundableComp = null)
    {
        if (!WoundableQuery.Resolve(woundableEntity, ref woundableComp)
            || !WoundableQuery.Resolve(parentWoundableEntity, ref parentWoundableComp))
            return;

        var bodyPart = Comp<BodyPartComponent>(parentWoundableEntity);
        if (!bodyPart.Body.HasValue)
            return;

        if (!Containers.TryGetContainingContainer(parentWoundableEntity, woundableEntity, out var container))
            return;

        woundableComp.WoundableSeverity = WoundableSeverity.Loss;

        if (TryComp<TargetingComponent>(bodyPart.Body.Value, out var targeting))
        {
            targeting.BodyStatus = GetWoundableStatesOnBodyPainFeels(bodyPart.Body.Value);
            Dirty(bodyPart.Body.Value, targeting);

            RaiseNetworkEvent(new TargetIntegrityChangeEvent(GetNetEntity(bodyPart.Body.Value)), bodyPart.Body.Value);
        }

        var childBodyPart = Comp<BodyPartComponent>(woundableEntity);
        if (TryComp<InventoryComponent>(bodyPart.Body, out var inventory)
            && Body.GetBodyPartCount(bodyPart.Body.Value, bodyPart.PartType) == 1
            && Body.TryGetPartSlotContainerName(childBodyPart.PartType, out var containerNames))
        {
            foreach (var containerName in containerNames)
            {
                Inventory.DropSlotContents(bodyPart.Body.Value, containerName, inventory);
            }
        }

        if (childBodyPart.PartType is BodyPartType.Hand or BodyPartType.Arm)
        {
            // Prevent anomalous behaviour
            Hands.TryDrop(bodyPart.Body.Value, woundableEntity);
        }

        // Still does the funny popping, if the children are critted. for the funny :3
        DestroyWoundableChildren(woundableEntity, woundableComp);

        Dirty(woundableEntity, woundableComp);

        var bodyPartId = container.ID;
        Body.DetachPart(
            parentWoundableEntity,
            SharedBodySystem.GetPartSlotContainerIdFromContainer(bodyPartId),
            woundableEntity,
            bodyPart,
            childBodyPart);
    }

    #endregion

    #region Wound Healing

    [PublicAPI]
    public override void ForceHealWoundsOnWoundable(EntityUid woundable,
        out FixedPoint2 healed,
        DamageGroupPrototype? damageGroup = null,
        WoundableComponent? component = null)
    {
        healed = 0;
        if (!Resolve(woundable, ref component))
            return;

        var woundsToHeal =
            GetWoundableWounds(woundable, component)
                .Where(wound => damageGroup == null || wound.Comp.DamageGroup == damageGroup)
                .ToList();

        foreach (var wound in woundsToHeal)
        {
            healed += wound.Comp.WoundSeverityPoint;
            RemoveWound(wound, wound);
        }

        UpdateWoundableIntegrity(woundable, component);
        CheckWoundableSeverityThresholds(woundable, component);
    }

    [PublicAPI]
    public override bool TryHealWoundsOnWoundable(EntityUid woundable,
        FixedPoint2 healAmount,
        string damageType,
        out FixedPoint2 healed,
        WoundableComponent? component = null,
        bool ignoreMultipliers = false)
    {
        healed = 0;
        if (!Resolve(woundable, ref component) || component.Wounds == null)
            return false;

        var woundsToHeal =
            (from wound in component.Wounds.ContainedEntities
                let woundComp = Comp<WoundComponent>(wound)
                where CanHealWound(wound)
                where damageType == woundComp.DamageType
                select (wound, woundComp)).Select(dummy => (Entity<WoundComponent>) dummy)
            .ToList();

        if (woundsToHeal.Count == 0)
            return false;

        var healNumba = healAmount / woundsToHeal.Count;
        var actualHeal = FixedPoint2.Zero;
        foreach (var wound in woundsToHeal)
        {
            var heal = ignoreMultipliers
                ? ApplyHealingRateMultipliers((wound,wound), woundable, -healNumba, component)
                : -healNumba;

            actualHeal += -heal;
            ApplyWoundSeverity(wound, heal, wound);
        }

        UpdateWoundableIntegrity(woundable, component);
        CheckWoundableSeverityThresholds(woundable, component);

        healed = actualHeal;
        return actualHeal > 0;
    }

    [PublicAPI]
    public override bool TryHealWoundsOnWoundable(EntityUid woundable,
        FixedPoint2 healAmount,
        out FixedPoint2 healed,
        WoundableComponent? component = null,
        DamageGroupPrototype? damageGroup = null,
        bool ignoreMultipliers = false)
    {
        healed = 0;
        if (!Resolve(woundable, ref component) || component.Wounds == null)
            return false;

        var woundsToHeal =
            (from wound in component.Wounds.ContainedEntities
                let woundComp = Comp<WoundComponent>(wound)
                where CanHealWound(wound)
                where damageGroup == null || damageGroup == woundComp.DamageGroup
                select (wound, woundComp)).Select(dummy => (Entity<WoundComponent>) dummy)
            .ToList(); // that's what I call LINQ.

        if (woundsToHeal.Count == 0)
            return false;

        var healNumba = healAmount / woundsToHeal.Count;
        var actualHeal = FixedPoint2.Zero;
        foreach (var wound in woundsToHeal)
        {
            var heal = ignoreMultipliers
                ? ApplyHealingRateMultipliers((wound,wound), woundable, -healNumba, component)
                : -healNumba;

            actualHeal += -heal;
            ApplyWoundSeverity(wound, heal, wound);
        }

        UpdateWoundableIntegrity(woundable, component);
        CheckWoundableSeverityThresholds(woundable, component);

        healed = actualHeal;
        return actualHeal > 0;
    }

    #endregion

    #region Private API

    private bool AddWound(
        EntityUid target,
        EntityUid wound,
        FixedPoint2 woundSeverity,
        DamageGroupPrototype? damageGroup,
        WoundableComponent? woundableComponent = null,
        WoundComponent? woundComponent = null)
    {
        if (!WoundableQuery.Resolve(target, ref woundableComponent, false)
            || !WoundQuery.Resolve(wound, ref woundComponent, false)
            || woundableComponent.Wounds == null
            || woundableComponent.Wounds.Contains(wound))
            return false;

        if (!woundableComponent.AllowWounds)
            return false;

        if (woundSeverity <= WoundThresholds[WoundSeverity.Healed])
            return false;

        Xform.SetParent(wound, target);
        woundComponent.HoldingWoundable = target;
        woundComponent.DamageGroup = damageGroup;

        if (!Containers.Insert(wound, woundableComponent.Wounds))
            return false;

        SetWoundSeverity(wound, woundSeverity, woundComponent);

        var woundMeta = MetaData(wound);
        var targetMeta = MetaData(target);

        Log.Debug($"Wound: {woundMeta.EntityPrototype!.ID}({wound}) created on {targetMeta.EntityPrototype!.ID}({target})");

        Dirty(wound, woundComponent);
        Dirty(target, woundableComponent);

        return true;
    }

    protected override bool RemoveWound(EntityUid woundEntity, WoundComponent? wound = null)
    {
        if (!WoundQuery.Resolve(woundEntity, ref wound, false)
            || !WoundableQuery.TryComp(wound.HoldingWoundable, out var woundable))
            return false;

        Log.Debug($"Wound: {MetaData(woundEntity).EntityPrototype!.ID}({woundEntity}) removed on {MetaData(wound.HoldingWoundable).EntityPrototype!.ID}({wound.HoldingWoundable})");

        UpdateWoundableIntegrity(wound.HoldingWoundable, woundable);
        CheckWoundableSeverityThresholds(wound.HoldingWoundable, woundable);

        Containers.Remove(woundEntity, woundable.Wounds!, false, true);
        return true;
    }

    private void TransferWoundDamage(
        EntityUid parent,
        EntityUid severed,
        EntityUid wound,
        WoundableComponent? parentWoundableComp = null,
        WoundComponent? woundComp = null)
    {
        if (!WoundableQuery.Resolve(parent, ref parentWoundableComp, false)
            || !WoundQuery.Resolve(wound, ref woundComp, false))
            return;

        TryInduceWound(
            parent,
            woundComp.DamageType,
            woundComp.WoundSeverityPoint * _woundTransferPart,
            out _,
            parentWoundableComp);

        var bodyPart = Comp<BodyPartComponent>(severed);
        foreach (var woundEnt in GetWoundableWounds(parent, parentWoundableComp))
        {
            if (woundEnt.Comp.DamageType != woundComp.DamageType)
                continue;

            var tourniquetable = EnsureComp<TourniquetableComponent>(woundEnt);
            tourniquetable.SeveredSymmetry = bodyPart.Symmetry;
            tourniquetable.SeveredPartType = bodyPart.PartType;
        }
    }

    #endregion
}
