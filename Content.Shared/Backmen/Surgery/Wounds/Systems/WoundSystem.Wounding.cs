﻿using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Shared.Backmen.CCVar;
using Content.Shared.Backmen.Surgery.Consciousness.Systems;
using Content.Shared.Backmen.Surgery.Pain.Components;
using Content.Shared.Backmen.Surgery.Traumas.Components;
using Content.Shared.Backmen.Surgery.Wounds.Components;
using Content.Shared.Backmen.Targeting;
using Content.Shared.Body.Components;
using Content.Shared.Body.Part;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.FixedPoint;
using Content.Shared.Gibbing.Events;
using Content.Shared.Popups;
using JetBrains.Annotations;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Shared.Backmen.Surgery.Wounds.Systems;

[Virtual]
public partial class WoundSystem
{
    private const string WoundContainerId = "Wounds";
    private const string BoneContainerId = "Bone";

    private const string WoundableDestroyalIdentifier = "WoundableDestroyal";

    private float _dodgeDistanceChance;
    private float _dodgeDistanceChange;

    protected EntityQuery<WoundComponent> WoundQuery;
    protected EntityQuery<WoundableComponent> WoundableQuery;

    private void InitWounding()
    {
        SubscribeLocalEvent<WoundableComponent, ComponentInit>(OnWoundableInit);
        SubscribeLocalEvent<WoundableComponent, MapInitEvent>(OnWoundableMapInit);

        SubscribeLocalEvent<WoundableComponent, HandleCustomDamage>(OnWoundableDamaged, after: [typeof(ConsciousnessSystem)]);

        SubscribeLocalEvent<WoundableComponent, EntInsertedIntoContainerMessage>(OnWoundableInserted);
        SubscribeLocalEvent<WoundableComponent, EntRemovedFromContainerMessage>(OnWoundableRemoved);

        SubscribeLocalEvent<WoundComponent, EntGotInsertedIntoContainerMessage>(OnWoundInserted);
        SubscribeLocalEvent<WoundComponent, EntGotRemovedFromContainerMessage>(OnWoundRemoved);

        SubscribeLocalEvent<WoundableComponent, AttemptEntityContentsGibEvent>(OnWoundableContentsGibAttempt);

        SubscribeLocalEvent<WoundComponent, WoundSeverityChangedEvent>(OnWoundSeverityChanged);
        SubscribeLocalEvent<WoundableComponent, WoundableSeverityChangedEvent>(OnWoundableSeverityChanged);

        SubscribeLocalEvent<WoundableComponent, BeforeDamageChangedEvent>(CheckDodge);
        SubscribeLocalEvent<WoundableComponent, WoundHealAttemptOnWoundableEvent>(HealWoundsOnWoundableAttempt);

        Subs.CVar(Cfg, CCVars.DodgeDistanceChance, val => _dodgeDistanceChance = val, true);
        Subs.CVar(Cfg, CCVars.DodgeDistanceChange, val => _dodgeDistanceChange = val, true);

        WoundQuery = GetEntityQuery<WoundComponent>();
        WoundableQuery = GetEntityQuery<WoundableComponent>();
    }

    #region Event Handling

    private void OnWoundableInit(EntityUid uid, WoundableComponent woundable, ComponentInit args)
    {
        woundable.RootWoundable = uid;

        woundable.Wounds = Containers.EnsureContainer<Container>(uid, WoundContainerId);
        woundable.Bone = Containers.EnsureContainer<Container>(uid, BoneContainerId);
    }

    private void OnWoundableMapInit(EntityUid uid, WoundableComponent woundable, MapInitEvent args)
    {
        var bone = Spawn(woundable.BoneEntity);
        if (!TryComp<BoneComponent>(bone, out var boneComp))
            return;

        Xform.SetParent(bone, uid);
        Containers.Insert(bone, woundable.Bone);

        boneComp.BoneWoundable = uid;
    }

    private void OnWoundInserted(EntityUid uid, WoundComponent comp, EntGotInsertedIntoContainerMessage args)
    {
        if (comp.HoldingWoundable == EntityUid.Invalid)
            return;

        var parentWoundable = WoundableQuery.Comp(comp.HoldingWoundable);
        var woundableRoot = WoundableQuery.Comp(parentWoundable.RootWoundable);

        var ev = new WoundAddedEvent(comp, parentWoundable, woundableRoot);
        RaiseLocalEvent(uid, ref ev);

        var ev1 = new WoundAddedEvent(comp, parentWoundable, woundableRoot);
        RaiseLocalEvent(comp.HoldingWoundable, ref ev1);
    }

    private void OnWoundRemoved(EntityUid woundableEntity, WoundComponent wound, EntGotRemovedFromContainerMessage args)
    {
        if (wound.HoldingWoundable == EntityUid.Invalid)
            return;

        if (!TryComp(wound.HoldingWoundable, out WoundableComponent? oldParentWoundable) ||
            !TryComp(oldParentWoundable.RootWoundable, out WoundableComponent? oldWoundableRoot))
            return;

        wound.HoldingWoundable = EntityUid.Invalid;

        var ev2 = new WoundRemovedEvent(wound, oldParentWoundable, oldWoundableRoot);
        RaiseLocalEvent(wound.HoldingWoundable, ref ev2);

        if (_net.IsServer && !IsClientSide(woundableEntity))
            QueueDel(woundableEntity);
    }

    private void OnWoundableDamaged(
        EntityUid woundable,
        WoundableComponent component,
        ref HandleCustomDamage args)
    {
        if (args.Handled)
            return;

        var bodyPart = Comp<BodyPartComponent>(woundable);
        if (bodyPart.Body.HasValue)
        {
            var before = new BeforeDamageChangedEvent(args.Damage, args.Origin, args.CanBeCancelled); // heheheha
            RaiseLocalEvent(bodyPart.Body.Value, ref before);

            if (before.Cancelled)
                return;
        }

        args.Damage = GetWoundsChanged(woundable, args.Origin, args.Damage, component: component);
        args.Handled = true;
    }

    private void OnWoundableInserted(EntityUid parentEntity, WoundableComponent parentWoundable, EntInsertedIntoContainerMessage args)
    {
        if (!TryComp<WoundableComponent>(args.Entity, out var childWoundable) || !_net.IsServer)
            return;

        InternalAddWoundableToParent(parentEntity, args.Entity, parentWoundable, childWoundable);
    }

    private void OnWoundableRemoved(EntityUid parentEntity, WoundableComponent parentWoundable, EntRemovedFromContainerMessage args)
    {
        if (!TryComp<WoundableComponent>(args.Entity, out var childWoundable) || !_net.IsServer)
            return;

        InternalRemoveWoundableFromParent(parentEntity, args.Entity, parentWoundable, childWoundable);
    }

    private void OnWoundableSeverityChanged(EntityUid uid, WoundableComponent component, WoundableSeverityChangedEvent args)
    {
        if (args.NewSeverity != WoundableSeverity.Loss)
            return;

        if (IsWoundableRoot(uid, component))
        {
            DestroyWoundable(uid, uid, component);
        }
        else
        {
            if (component.ParentWoundable != null && Comp<BodyPartComponent>(uid).Body != null)
            {
                DestroyWoundable(component.ParentWoundable.Value, uid, component);
            }
            else
            {
                // it will be destroyed.
                DestroyWoundable(uid, uid, component);
            }
        }
    }

    private void OnWoundableContentsGibAttempt(EntityUid uid, WoundableComponent comp, ref AttemptEntityContentsGibEvent args)
    {
        if (args.ExcludedContainers == null)
        {
            args.ExcludedContainers = new List<string> { WoundContainerId, BoneContainerId };
        }
        else
        {
            args.ExcludedContainers.AddRange(new List<string> { WoundContainerId, BoneContainerId });
        }
    }

    private void CheckDodge(EntityUid uid, WoundableComponent comp, BeforeDamageChangedEvent args)
    {
        if (!args.CanBeCancelled)
            return;

        if (args.Damage.GetTotal() <= 0 || _dodgeDistanceChance <= 0)
            return;

        var chance = comp.DodgeChance;

        var bodyPart = Comp<BodyPartComponent>(uid);
        if (args.Origin != null && bodyPart.Body != null)
        {
            var bodyTransform = Xform.GetWorldPosition(bodyPart.Body.Value);
            var originTransform = Xform.GetWorldPosition(args.Origin.Value);

            var distance = (originTransform - bodyTransform).Length();
            if (distance < _dodgeDistanceChance * 2)
            {
                chance = 0;
            }
            else
            {
                var additionalChance =
                    distance
                    / _dodgeDistanceChance // 1 letter difference
                    * _dodgeDistanceChange;

                chance += additionalChance;
            }
        }

        if (!Random.Prob(Math.Clamp((float) chance, 0, 1)))
            return;

        if (bodyPart.Body.HasValue)
        {
            // Critted or dead people of course can't dodge for shit.
            if (!_mobState.IsAlive(bodyPart.Body.Value))
                return;

            _popup.PopupEntity(Loc.GetString("woundable-dodged", ("entity", bodyPart.Body.Value)), bodyPart.Body.Value, PopupType.Medium);
        }

        args.Cancelled = true;
    }

    private void HealWoundsOnWoundableAttempt(Entity<WoundableComponent> woundable, ref WoundHealAttemptOnWoundableEvent args)
    {
        if (woundable.Comp.WoundableSeverity == WoundableSeverity.Loss)
            args.Cancelled = true;
    }

    private void OnWoundSeverityChanged(EntityUid wound, WoundComponent woundComponent, WoundSeverityChangedEvent args)
    {
        if (args.NewSeverity != WoundSeverity.Healed)
            return;

        TryMakeScar(wound, out _, woundComponent);
        RemoveWound(wound, woundComponent);
    }

    #endregion

    #region Public API

    [PublicAPI]
    public virtual bool TryInduceWounds(
        EntityUid uid,
        WoundSpecifier wounds,
        out List<Entity<WoundComponent>> woundsInduced,
        WoundableComponent? woundable = null)
    {
        // Server-only execution
        woundsInduced = new List<Entity<WoundComponent>>();
        return false;
    }

    [PublicAPI]
    public virtual bool TryInduceWound(
        EntityUid uid,
        string woundId,
        FixedPoint2 severity,
        [NotNullWhen(true)] out Entity<WoundComponent>? woundInduced,
        WoundableComponent? woundable = null)
    {
        // Server-only execution
        woundInduced = null;
        return false;
    }

    [PublicAPI]
    public bool CanAddWound(
        EntityUid uid,
        string id,
        FixedPoint2 severity,
        WoundableComponent? woundable = null)
    {
        if (!IsWoundPrototypeValid(id))
            return false;

        if (!WoundableQuery.Resolve(uid, ref woundable))
            return false;

        if (woundable.Wounds == null)
            return false;

        if (!woundable.AllowWounds)
            return false;

        if (severity <= WoundThresholds[WoundSeverity.Healed])
            return false;

        return true;
    }

    /// <summary>
    /// Opens a new wound on a requested woundable.
    /// </summary>
    /// <param name="uid">UID of the woundable (body part).</param>
    /// <param name="woundProtoId">Wound prototype.</param>
    /// <param name="severity">Severity for wound to apply.</param>
    /// <param name="woundCreated">The wound that was created</param>
    /// <param name="damageGroup"></param>
    /// <param name="woundable">Woundable component.</param>
    [PublicAPI]
    public virtual bool TryCreateWound(
         EntityUid uid,
         string woundProtoId,
         FixedPoint2 severity,
         [NotNullWhen(true)] out Entity<WoundComponent>? woundCreated,
         DamageGroupPrototype? damageGroup = null,
         WoundableComponent? woundable = null)
    {
        // Server-only execution
        woundCreated = null;
        return false;
    }

    [PublicAPI]
    public bool CanContinueWound(
        EntityUid uid,
        string id,
        FixedPoint2 severity,
        [NotNullWhen(true)] out Entity<WoundComponent>? continuableWound,
        WoundableComponent? woundable = null)
    {
        continuableWound = null;
        if (!IsWoundPrototypeValid(id))
            return false;

        if (!WoundableQuery.Resolve(uid, ref woundable))
            return false;

        if (woundable.Wounds == null)
            return false;

        var proto = _prototype.Index(id);
        foreach (var wound in GetWoundableWounds(uid, woundable))
        {
            if (proto.ID != wound.Comp.DamageType)
                continue;

            continuableWound = wound;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Continues wound with specific type, if there's any. Adds severity to it basically.
    /// </summary>
    /// <param name="uid">Woundable entity's UID.</param>
    /// <param name="id">Wound entity's ID.</param>
    /// <param name="severity">Severity to apply.</param>
    /// <param name="woundContinued">The wound the severity was applied to, if any</param>
    /// <param name="woundable">Woundable for wound to add.</param>
    /// <returns>Returns true, if wound was continued.</returns>
    [PublicAPI]
    public virtual bool TryContinueWound(
        EntityUid uid,
        string id,
        FixedPoint2 severity,
        [NotNullWhen(true)] out Entity<WoundComponent>? woundContinued,
        WoundableComponent? woundable = null)
    {
        // Server-only execution
        woundContinued = null;
        return false;
    }

    /// <summary>
    /// Tries to create a scar on a woundable entity. Takes a scar prototype from WoundComponent.
    /// </summary>
    /// <param name="wound">The wound entity, from which the scar will be made.</param>
    /// <param name="scarWound">The result scar wound, if created.</param>
    /// <param name="woundComponent">The WoundComponent representing a specific wound.</param>
    [PublicAPI]
    public virtual bool TryMakeScar(EntityUid wound, [NotNullWhen(true)] out Entity<WoundComponent>? scarWound, WoundComponent? woundComponent = null)
    {
        // Server-only execution
        scarWound = null;
        return false;
    }

    /// <summary>
    /// Sets severity of a wound.
    /// </summary>
    /// <param name="uid">UID of the wound.</param>
    /// <param name="severity">Severity to set.</param>
    /// <param name="wound">Wound to which severity is applied.</param>
    [PublicAPI]
    public virtual void SetWoundSeverity(EntityUid uid, FixedPoint2 severity, WoundComponent? wound = null)
    {
        // Server-only execution
    }

    /// <summary>
    /// Applies severity to a wound
    /// </summary>
    /// <param name="uid">UID of the wound.</param>
    /// <param name="severity">Severity to add.</param>
    /// <param name="wound">Wound to which severity is applied.</param>
    [PublicAPI]
    public virtual void ApplyWoundSeverity(
        EntityUid uid,
        FixedPoint2 severity,
        WoundComponent? wound = null)
    {
        // Server-only execution
    }

    [PublicAPI]
    public FixedPoint2 ApplySeverityModifiers(
        EntityUid woundable,
        FixedPoint2 severity,
        WoundableComponent? component = null)
    {
        if (!WoundableQuery.Resolve(woundable, ref component))
            return severity;

        if (component.SeverityMultipliers.Count == 0)
            return severity;

        var toMultiply =
            component.SeverityMultipliers.Sum(multiplier => (float) multiplier.Value.Change) / component.SeverityMultipliers.Count;
        return severity * toMultiply;
    }

    [PublicAPI]
    public DamageSpecifier GetWoundsChanged(
        EntityUid woundable,
        EntityUid? origin,
        DamageSpecifier damage,
        bool performLogic = true,
        WoundableComponent? component = null)
    {
        if (!WoundableQuery.Resolve(woundable, ref component, false))
            return new DamageSpecifier(); // Empty

        var damageIncreased = false;
        var actuallyInducedDamage = new DamageSpecifier(damage);

        var woundsToAdd = new Dictionary<string, FixedPoint2>();
        var addedWounds = new List<Entity<WoundComponent>>();
        var removedWounds = new List<Entity<WoundComponent>>();

        var changedWounds = new Dictionary<Entity<WoundComponent>, FixedPoint2>();
        var totalChange = FixedPoint2.Zero;

        foreach (var damagePiece in damage.DamageDict)
        {
            if (TryGetWoundOfDamageType(woundable, damagePiece.Key, out var foundWound, component))
            {
                // Healing ignores severity modifiers
                var severityApplied = damagePiece.Value > 0
                    ? ApplySeverityModifiers(woundable, damagePiece.Value, component)
                    : damagePiece.Value;

                if (severityApplied < 0 && -severityApplied > foundWound.Value.Comp.WoundSeverityPoint)
                {
                    actuallyInducedDamage.DamageDict[damagePiece.Key] = -foundWound.Value.Comp.WoundSeverityPoint;

                    removedWounds.Add(foundWound.Value);
                    changedWounds.Add(foundWound.Value, -foundWound.Value.Comp.WoundSeverityPoint);
                    totalChange -= foundWound.Value.Comp.WoundSeverityPoint;
                }
                else
                {
                    if (!CanContinueWound(
                            woundable,
                            damagePiece.Key,
                            damagePiece.Value,
                            out var continuedWound,
                            component))
                        continue;

                    var oldSeverity = continuedWound.Value.Comp.WoundSeverityPoint - severityApplied;
                    var severityDelta = continuedWound.Value.Comp.WoundSeverityPoint - oldSeverity;

                    actuallyInducedDamage.DamageDict[damagePiece.Key] = severityDelta;
                    if (severityApplied > 0)
                        damageIncreased = true;

                    changedWounds.Add(continuedWound.Value, severityDelta);
                    totalChange += severityDelta;
                }
            }
            else
            {
                if (damagePiece.Value <= 0)
                    continue;

                if (!CanAddWound(
                        woundable,
                        damagePiece.Key,
                        damagePiece.Value,
                        component))
                    continue;

                var severity = ApplySeverityModifiers(woundable, damagePiece.Value, component);

                actuallyInducedDamage.DamageDict[damagePiece.Key] = severity;
                damageIncreased = true;

                woundsToAdd.Add(damagePiece.Key, damagePiece.Value);
                totalChange += severity;
            }
        }

        if (performLogic)
        {
            foreach (var woundToAdd in woundsToAdd)
            {
                if (TryCreateWound(
                        woundable,
                        woundToAdd.Key,
                        woundToAdd.Value,
                        out var woundCreated))
                {
                    addedWounds.Add(woundCreated.Value);
                }
            }
        }

        var woundsDeltaEv = new WoundsDeltaChanged(origin, totalChange, changedWounds, damageIncreased);
        RaiseLocalEvent(woundable, ref woundsDeltaEv);

        foreach (var wound in
                 changedWounds.Where(wound => addedWounds.Contains(wound.Key) || removedWounds.Contains(wound.Key)))
        {
            changedWounds.Remove(wound.Key);
        }

        if (performLogic)
        {
            foreach (var woundToRemove in removedWounds)
            {
                RemoveWound(woundToRemove);
            }

            foreach (var woundToChange in changedWounds)
            {
                ApplyWoundSeverity(woundToChange.Key, woundToChange.Value, woundToChange.Key);
            }
        }

        var woundsChangedEv = new WoundsChangedEvent(origin, addedWounds, removedWounds, changedWounds, damageIncreased);
        RaiseLocalEvent(woundable, ref woundsChangedEv);

        return actuallyInducedDamage;
    }

    /// <summary>
    /// Applies severity multiplier to a wound.
    /// </summary>
    /// <param name="uid">UID of the woundable.</param>
    /// <param name="owner">UID of the multiplier owner.</param>
    /// <param name="change">The severity multiplier itself</param>
    /// <param name="identifier">A string to defy this multiplier from others.</param>
    /// <param name="component">Woundable to which severity multiplier is applied.</param>
    [PublicAPI]
    public virtual bool TryAddWoundableSeverityMultiplier(
        EntityUid uid,
        EntityUid owner,
        FixedPoint2 change,
        string identifier,
        WoundableComponent? component = null)
    {
        // Server-only execution
        return false;
    }

    /// <summary>
    /// Removes a multiplier from a woundable.
    /// </summary>
    /// <param name="uid">UID of the woundable.</param>
    /// <param name="identifier">Identifier of the said multiplier.</param>
    /// <param name="component">Woundable to which severity multiplier is applied.</param>
    [PublicAPI]
    public virtual bool TryRemoveWoundableSeverityMultiplier(
        EntityUid uid,
        string identifier,
        WoundableComponent? component = null)
    {
        // Server-only execution
        return false;
    }

    /// <summary>
    /// Changes a multiplier's change in a specified woundable.
    /// </summary>
    /// <param name="uid">UID of the woundable.</param>
    /// <param name="identifier">Identifier of the said multiplier.</param>
    /// <param name="change">The new multiplier fixed point.</param>
    /// <param name="component">Woundable to which severity multiplier is applied.</param>
    [PublicAPI]
    public virtual bool TryChangeWoundableSeverityMultiplier(
        EntityUid uid,
        string identifier,
        FixedPoint2 change,
        WoundableComponent? component = null)
    {
        // Server-only execution
        return false;
    }

    /// <summary>
    /// Destroys an entity's body part if conditions are met.
    /// </summary>
    /// <param name="parentWoundableEntity">Parent of the woundable entity. Yes.</param>
    /// <param name="woundableEntity">The entity containing the vulnerable body part</param>
    /// <param name="woundableComp">Woundable component of woundableEntity.</param>
    /// <param name="parentWoundableComp">Woundable component of parentWoundableEntity</param>
    public virtual void DestroyWoundable(
        EntityUid parentWoundableEntity,
        EntityUid woundableEntity,
        WoundableComponent? woundableComp = null,
        WoundableComponent? parentWoundableComp = null)
    {
        // Server-only execution :pray:
    }

    /// <summary>
    /// Amputates (not destroys) an entity's body part if conditions are met.
    /// </summary>
    /// <param name="parentWoundableEntity">Parent of the woundable entity. Yes.</param>
    /// <param name="woundableEntity">The entity containing the vulnerable body part</param>
    /// <param name="woundableComp">Woundable component of woundableEntity.</param>
    /// <param name="parentWoundableComp">Woundable component of parentWoundableEntity.</param>
    public virtual void AmputateWoundable(
        EntityUid parentWoundableEntity,
        EntityUid woundableEntity,
        WoundableComponent? woundableComp = null,
        WoundableComponent? parentWoundableComp = null)
    {
        // Server-only execution
    }

    /// <summary>
    /// Does whatever AmputateWoundable does, but does it without pain and the other mess.
    /// </summary>
    /// <param name="parentWoundableEntity">Parent of the woundable entity. Yes.</param>
    /// <param name="woundableEntity">The entity containing the vulnerable body part</param>
    /// <param name="woundableComp">Woundable component of woundableEntity.</param>
    /// <param name="parentWoundableComp">Woundable component of parentWoundableEntity.</param>
    public virtual void AmputateWoundableSafely(
        EntityUid parentWoundableEntity,
        EntityUid woundableEntity,
        WoundableComponent? woundableComp = null,
        WoundableComponent? parentWoundableComp = null)
    {
        // Server-only execution
    }

    #endregion

    #region Private API

    protected virtual bool RemoveWound(EntityUid woundEntity, WoundComponent? wound = null)
    {
        // Server-only execution
        return false;
    }

    #endregion

    #region Helpers

    /// <summary>
    /// Validates the wound prototype based on the given prototype ID.
    /// Checks if the specified prototype ID corresponds to a valid EntityPrototype in the collection,
    /// ensuring it contains the necessary WoundComponent.
    /// </summary>
    /// <param name="protoId">The prototype ID to be validated.</param>
    /// <returns>True if the wound prototype is valid, otherwise false.</returns>
    protected bool IsWoundPrototypeValid(string protoId)
    {
        return _prototype.TryIndex<EntityPrototype>(protoId, out var woundPrototype)
               && woundPrototype.TryGetComponent<WoundComponent>(out _, _factory);
    }

    public Dictionary<TargetBodyPart, WoundableSeverity> GetWoundableStatesOnBody(EntityUid body)
    {
        var result = new Dictionary<TargetBodyPart, WoundableSeverity>();

        foreach (var part in SharedTargetingSystem.GetValidParts())
        {
            result[part] = WoundableSeverity.Loss;
        }

        foreach (var (id, bodyPart) in Body.GetBodyChildren(body))
        {
            var target = Body.GetTargetBodyPart(bodyPart);
            if (target == null)
                continue;

            if (!WoundableQuery.TryComp(id, out var woundable))
                continue;

            result[target.Value] = woundable.WoundableSeverity;
        }

        return result;
    }

    public Dictionary<TargetBodyPart, WoundableSeverity> GetWoundableStatesOnBodyPainFeels(EntityUid body)
    {
        var result = new Dictionary<TargetBodyPart, WoundableSeverity>();

        foreach (var part in SharedTargetingSystem.GetValidParts())
        {
            result[part] = WoundableSeverity.Loss;
        }

        foreach (var (id, bodyPart) in Body.GetBodyChildren(body))
        {
            var target = Body.GetTargetBodyPart(bodyPart);
            if (target == null)
                continue;

            if (!WoundableQuery.TryComp(id, out var woundable) || !TryComp<NerveComponent>(id, out var nerve))
                continue;

            var damageFeeling = woundable.WoundableIntegrity * nerve.PainFeels;

            var nearestSeverity = woundable.WoundableSeverity;
            foreach (var (severity, value) in woundable.Thresholds.OrderByDescending(kv => kv.Value))
            {
                if (damageFeeling <= 0)
                {
                    nearestSeverity = WoundableSeverity.Loss;
                    break;
                }

                if (damageFeeling >= woundable.IntegrityCap)
                {
                    nearestSeverity = WoundableSeverity.Healthy;
                    break;
                }

                if (damageFeeling < value)
                    continue;

                nearestSeverity = severity;
                break;
            }

            result[target.Value] = nearestSeverity;
        }

        return result;
    }

    /// <summary>
    /// Check if this woundable is root
    /// </summary>
    /// <param name="woundableEntity">Owner of the woundable</param>
    /// <param name="woundable">woundable component</param>
    /// <returns>true if the woundable is the root of the hierarchy</returns>
    public bool IsWoundableRoot(EntityUid woundableEntity, WoundableComponent? woundable = null)
    {
        return WoundableQuery.Resolve(woundableEntity, ref woundable, false) && woundable.RootWoundable == woundableEntity;
    }

    public IEnumerable<Entity<WoundComponent>> GetBodyWounds(
        EntityUid body,
        BodyComponent? comp = null)
    {
        if (!Resolve(body, ref comp))
            yield break;

        var rootPart = comp.RootContainer.ContainedEntity;
        if (!rootPart.HasValue)
            yield break;

        foreach (var woundable in GetAllWoundableChildren(rootPart.Value))
        {
            foreach (var value in GetWoundableWounds(woundable, woundable))
            {
                yield return value;
            }
        }
    }

    public FixedPoint2 GetBodySeverityPoint(
        EntityUid body,
        BodyComponent? comp = null)
    {
        return !Resolve(body, ref comp)
                ? FixedPoint2.Zero
                : GetBodyWounds(body, comp).Aggregate(FixedPoint2.Zero, (current, wound) => current + wound.Comp.WoundSeverityPoint);
    }

    /// <summary>
    /// Gets all woundable children of a specified woundable
    /// </summary>
    /// <param name="targetEntity">Owner of the woundable</param>
    /// <param name="targetWoundable"></param>
    /// <returns>Enumerable to the found children</returns>
    public IEnumerable<Entity<WoundableComponent>> GetAllWoundableChildren(
        EntityUid targetEntity,
        WoundableComponent? targetWoundable = null)
    {
        if (!WoundableQuery.Resolve(targetEntity, ref targetWoundable, false))
            yield break;

        foreach (var childEntity in targetWoundable.ChildWoundables)
        {
            if (!TryComp(childEntity, out WoundableComponent? childWoundable))
                continue;
            foreach (var value in GetAllWoundableChildren(childEntity, childWoundable))
            {
                yield return value;
            }
        }

        yield return (targetEntity, targetWoundable);
    }

    /// <summary>
    /// Finds all children of a specified woundable that have a specific component
    /// </summary>
    /// <param name="targetEntity"></param>
    /// <param name="targetWoundable"></param>
    /// <typeparam name="T">the type of the component we want to find</typeparam>
    /// <returns>Enumerable to the found children</returns>
    public IEnumerable<Entity<WoundableComponent, T>> GetAllWoundableChildrenWithComp<T>(
        EntityUid targetEntity,
        WoundableComponent? targetWoundable = null) where T: Component, new()
    {
        if (!WoundableQuery.Resolve(targetEntity, ref targetWoundable, false))
            yield break;

        foreach (var (childEntity, childWoundable) in GetAllWoundableChildren(targetEntity, targetWoundable))
        {
            foreach (var value in GetAllWoundableChildrenWithComp<T>(childEntity, childWoundable))
            {
                yield return value;
            }
        }

        if (!TryComp(targetEntity, out T? foundComp))
            yield break;

        yield return (targetEntity, targetWoundable,foundComp);
    }

    /// <summary>
    /// Retrieves a wound of a specific damage type
    /// </summary>
    /// <param name="targetEntity"></param>
    /// <param name="wound"></param>
    /// <param name="targetWoundable"></param>
    /// <param name="damageType"></param>
    /// <returns>The said wound</returns>
    public bool TryGetWoundOfDamageType(
        EntityUid targetEntity,
        string damageType,
        [NotNullWhen(true)] out Entity<WoundComponent>? wound,
        WoundableComponent? targetWoundable = null)
    {
        wound = null;
        if (!WoundableQuery.Resolve(targetEntity, ref targetWoundable, false))
            return false;

        foreach (var fWound in GetWoundableWounds(targetEntity, targetWoundable))
        {
            if (fWound.Comp.DamageType != damageType)
                continue;

            wound = fWound;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Retrieves all wounds associated with a specified entity.
    /// </summary>
    /// <param name="targetEntity">The UID of the target entity.</param>
    /// <param name="targetWoundable">Optional: The WoundableComponent of the target entity.</param>
    /// <returns>An enumerable collection of tuples containing EntityUid and WoundComponent pairs.</returns>
    public IEnumerable<Entity<WoundComponent>> GetAllWounds(
        EntityUid targetEntity,
        WoundableComponent? targetWoundable = null)
    {
        if (!WoundableQuery.Resolve(targetEntity, ref targetWoundable, false))
            yield break;

        foreach (var (ent, childWoundable) in GetAllWoundableChildren(targetEntity, targetWoundable))
        {
            if (childWoundable.Wounds == null)
                continue;
            foreach (var woundEntity in GetWoundableWounds(ent, childWoundable))
            {
                yield return (woundEntity, woundEntity);
            }
        }
    }

    /// <summary>
    /// Retrieves all wounds associated with a specified entity, and a component.
    /// </summary>
    /// <param name="targetEntity">The UID of the target entity.</param>
    /// <param name="targetWoundable">Optional: The WoundableComponent of the target entity.</param>
    /// <returns>An enumerable collection of tuples containing EntityUid and WoundComponent, and T component pairs.</returns>
    public IEnumerable<Entity<WoundComponent, T>> GetAllWoundsWithComp<T>(
        EntityUid targetEntity,
        WoundableComponent? targetWoundable = null) where T: Component, new()
    {
        if (!WoundableQuery.Resolve(targetEntity, ref targetWoundable, false))
            yield break;

        foreach (var (ent, childWoundable) in GetAllWoundableChildren(targetEntity, targetWoundable))
        {
            if (childWoundable.Wounds == null)
                continue;
            foreach (var woundEntity in GetWoundableWoundsWithComp<T>(ent, childWoundable))
            {
                yield return woundEntity;
            }
        }
    }

    /// <summary>
    /// Get the wounds present on a specific woundable
    /// </summary>
    /// <param name="targetEntity">Entity that owns the woundable</param>
    /// <param name="targetWoundable">Woundable component</param>
    /// <returns>An enumerable pointing to one of the found wounds</returns>
    public IEnumerable<Entity<WoundComponent>> GetWoundableWounds(EntityUid targetEntity,
        WoundableComponent? targetWoundable = null)
    {
        if (!WoundableQuery.Resolve(targetEntity, ref targetWoundable, false)
            || targetWoundable.Wounds == null || targetWoundable.Wounds.Count == 0)
            yield break;

        foreach (var woundEntity in targetWoundable.Wounds.ContainedEntities.ToList())
        {
            yield return (woundEntity, WoundQuery.Comp(woundEntity));
        }
    }

    /// <summary>
    /// Get the wounds present on a specific woundable, with a component you want
    /// </summary>
    /// <param name="targetEntity">Entity that owns the woundable</param>
    /// <param name="targetWoundable">Woundable component</param>
    /// <returns>An enumerable pointing to one of the found wounds, with the said component</returns>
    public IEnumerable<Entity<WoundComponent, T>> GetWoundableWoundsWithComp<T>(
        EntityUid targetEntity,
        WoundableComponent? targetWoundable = null) where T: Component, new()
    {
        if (!WoundableQuery.Resolve(targetEntity, ref targetWoundable, false)
            || targetWoundable.Wounds == null || targetWoundable.Wounds.Count == 0)
            yield break;

        foreach (var woundEntity in GetWoundableWounds(targetEntity, targetWoundable))
        {
            if (!TryComp<T>(woundEntity, out var foundComponent))
                continue;

            yield return (woundEntity, woundEntity, foundComponent);
        }
    }

    /// <summary>
    /// Returns you the sum of all wounds on this woundable
    /// </summary>
    /// <param name="targetEntity">The woundable uid</param>
    /// <param name="targetWoundable">The component</param>
    /// <param name="damageGroup">The damage group of said wounds</param>
    /// <param name="healable">Are the wounds supposed to be healable</param>
    /// <returns>The severity sum</returns>
    public FixedPoint2 GetWoundableSeverityPoint(
        EntityUid targetEntity,
        WoundableComponent? targetWoundable = null,
        string? damageGroup = null,
        bool healable = false)
    {
        if (!WoundableQuery.Resolve(targetEntity, ref targetWoundable, false)
            || targetWoundable.Wounds == null || targetWoundable.Wounds.Count == 0)
            return FixedPoint2.Zero;

        if (healable)
        {
            return GetWoundableWounds(targetEntity, targetWoundable)
                .Where(wound => wound.Comp.DamageGroup?.ID == damageGroup || damageGroup == null)
                .Where(wound => CanHealWound(wound))
                .Aggregate(FixedPoint2.Zero, (current, wound) => current + wound.Comp.WoundSeverityPoint);
        }

        return GetWoundableWounds(targetEntity, targetWoundable)
            .Where(wound => wound.Comp.DamageGroup?.ID == damageGroup || damageGroup == null)
            .Aggregate(FixedPoint2.Zero, (current, wound) => current + wound.Comp.WoundSeverityPoint);
    }

    /// <summary>
    /// Returns the integrity damage the woundable has
    /// </summary>
    /// <param name="targetEntity">The woundable uid</param>
    /// <param name="targetWoundable">The component</param>
    /// <param name="damageGroup">The damage group of wounds that induced the damage</param>
    /// <param name="healable">Is the integrity damage healable</param>
    /// <returns>The integrity damage</returns>
    public FixedPoint2 GetWoundableIntegrityDamage(
        EntityUid targetEntity,
        WoundableComponent? targetWoundable = null,
        string? damageGroup = null,
        bool healable = false)
    {
        if (!WoundableQuery.Resolve(targetEntity, ref targetWoundable, false)
            || targetWoundable.Wounds == null || targetWoundable.Wounds.Count == 0)
            return FixedPoint2.Zero;

        if (healable)
        {
            return GetWoundableWounds(targetEntity, targetWoundable)
                .Where(wound => wound.Comp.DamageGroup?.ID == damageGroup || damageGroup == null)
                .Where(wound => CanHealWound(wound))
                .Aggregate(FixedPoint2.Zero, (current, wound) => current + wound.Comp.WoundIntegrityDamage);
        }

        return GetWoundableWounds(targetEntity, targetWoundable)
            .Where(wound => wound.Comp.DamageGroup?.ID == damageGroup || damageGroup == null)
            .Aggregate(FixedPoint2.Zero, (current, wound) => current + wound.Comp.WoundIntegrityDamage);
    }

    #endregion
}
