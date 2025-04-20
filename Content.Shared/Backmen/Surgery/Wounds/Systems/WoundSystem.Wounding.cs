using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Shared.Backmen.CCVar;
using Content.Shared.Backmen.Surgery.Pain.Components;
using Content.Shared.Backmen.Surgery.Traumas;
using Content.Shared.Backmen.Surgery.Traumas.Components;
using Content.Shared.Backmen.Surgery.Wounds.Components;
using Content.Shared.Backmen.Targeting;
using Content.Shared.Body.Components;
using Content.Shared.Body.Part;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.FixedPoint;
using Content.Shared.Gibbing.Events;
using Content.Shared.Humanoid;
using Content.Shared.Inventory;
using Content.Shared.Popups;
using JetBrains.Annotations;
using Robust.Shared.Containers;
using Robust.Shared.Player;
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
    private float _woundTransferPart;

    protected EntityQuery<WoundComponent> _woundQuery;
    protected EntityQuery<WoundableComponent> _woundableQuery;

    private void InitWounding()
    {
        SubscribeLocalEvent<WoundableComponent, ComponentInit>(OnWoundableInit);
        SubscribeLocalEvent<WoundableComponent, MapInitEvent>(OnWoundableMapInit);

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
        Subs.CVar(Cfg, CCVars.WoundTransferPart, val => _woundTransferPart = val, true);

        _woundQuery = GetEntityQuery<WoundComponent>();
        _woundableQuery = GetEntityQuery<WoundableComponent>();
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

        var parentWoundable = _woundableQuery.Comp(comp.HoldingWoundable);
        var woundableRoot = _woundableQuery.Comp(parentWoundable.RootWoundable);

        var ev = new WoundAddedEvent(comp, parentWoundable, woundableRoot);
        RaiseLocalEvent(uid, ref ev);

        var ev1 = new WoundAddedEvent(comp, parentWoundable, woundableRoot);
        RaiseLocalEvent(comp.HoldingWoundable, ref ev1);

        var bodyPart = Comp<BodyPartComponent>(comp.HoldingWoundable);
        if (bodyPart.Body.HasValue)
        {
            var ev2 = new WoundAddedOnBodyEvent((uid, comp), parentWoundable, woundableRoot);
            RaiseLocalEvent(bodyPart.Body.Value, ref ev2);
        }
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
        // 'terminatingordeleted', might fix the random magic blood splatters but well
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

        if (args.Damage.GetTotal() <= 0)
            return;

        var chance = comp.DodgeChance;

        var bodyPart = Comp<BodyPartComponent>(uid);
        if (args.Origin != null)
        {
            if (bodyPart.Body != null)
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

    protected virtual bool RemoveWound(EntityUid woundEntity, WoundComponent? wound = null)
    {
        // Server-only execution
        return false;
    }

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

    /// <summary>
    /// Opens a new wound on a requested woundable.
    /// </summary>
    /// <param name="uid">UID of the woundable (body part).</param>
    /// <param name="woundProtoId">Wound prototype.</param>
    /// <param name="severity">Severity for wound to apply.</param>
    /// <param name="woundCreated">The wound that was created</param>
    /// <param name="damageGroup">Damage group.</param>
    /// <param name="woundable">Woundable component.</param>
    [PublicAPI]
    public virtual bool TryCreateWound(
         EntityUid uid,
         string woundProtoId,
         FixedPoint2 severity,
         [NotNullWhen(true)] out Entity<WoundComponent>? woundCreated,
         DamageGroupPrototype? damageGroup,
         WoundableComponent? woundable = null)
    {
        // Server-only execution
        woundCreated = null;
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
        if (!_woundableQuery.Resolve(woundable, ref component))
            return severity;

        if (component.SeverityMultipliers.Count == 0)
            return severity;

        var toMultiply =
            component.SeverityMultipliers.Sum(multiplier => (float) multiplier.Value.Change) / component.SeverityMultipliers.Count;
        return severity * toMultiply;
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
    public bool TryAddWoundableSeverityMultiplier(
        EntityUid uid,
        EntityUid owner,
        FixedPoint2 change,
        string identifier,
        WoundableComponent? component = null)
    {
        if (!_woundableQuery.Resolve(uid, ref component) || component.Wounds == null)
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

    /// <summary>
    /// Removes a multiplier from a woundable.
    /// </summary>
    /// <param name="uid">UID of the woundable.</param>
    /// <param name="identifier">Identifier of the said multiplier.</param>
    /// <param name="component">Woundable to which severity multiplier is applied.</param>
    [PublicAPI]
    public bool TryRemoveWoundableSeverityMultiplier(
        EntityUid uid,
        string identifier,
        WoundableComponent? component = null)
    {
        if (!_woundableQuery.Resolve(uid, ref component) || component.Wounds == null)
            return false;

        foreach (var multiplier in component.SeverityMultipliers.Where(multiplier => multiplier.Value.Identifier == identifier))
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

    /// <summary>
    /// Changes a multiplier's change in a specified woundable.
    /// </summary>
    /// <param name="uid">UID of the woundable.</param>
    /// <param name="identifier">Identifier of the said multiplier.</param>
    /// <param name="change">The new multiplier fixed point.</param>
    /// <param name="component">Woundable to which severity multiplier is applied.</param>
    [PublicAPI]
    public bool TryChangeWoundableSeverityMultiplier(
        EntityUid uid,
        string identifier,
        FixedPoint2 change,
        WoundableComponent? component = null)
    {
        if (!_woundableQuery.Resolve(uid, ref component) || component.Wounds == null)
            return false;

        foreach (var multiplier in component.SeverityMultipliers.Where(multiplier => multiplier.Value.Identifier == identifier).ToList())
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

    /// <summary>
    /// Destroys an entity's body part if conditions are met.
    /// </summary>
    /// <param name="parentWoundableEntity">Parent of the woundable entity. Yes.</param>
    /// <param name="woundableEntity">The entity containing the vulnerable body part</param>
    /// <param name="woundableComp">Woundable component of woundableEntity.</param>
    public void DestroyWoundable(EntityUid parentWoundableEntity, EntityUid woundableEntity, WoundableComponent woundableComp)
    {
        var bodyPart = Comp<BodyPartComponent>(woundableEntity);
        if (bodyPart.Body == null)
        {
            DropWoundableOrgans(woundableEntity, woundableComp);
            if (_net.IsServer && !IsClientSide(woundableEntity))
                QueueDel(woundableEntity);

            // TODO: Some cool effect when the limb gets destroyed
        }
        else
        {
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

                if (_net.IsServer)
                    RaiseNetworkEvent(new TargetIntegrityChangeEvent(GetNetEntity(bodyPart.Body.Value)), bodyPart.Body.Value);
            }

            _audio.PlayPvs(woundableComp.WoundableDestroyedSound, bodyPart.Body.Value);
            Dirty(woundableEntity, woundableComp);

            if (IsWoundableRoot(woundableEntity, woundableComp))
            {
                DropWoundableOrgans(woundableEntity, woundableComp);
                DestroyWoundableChildren(woundableEntity, woundableComp);

                if (_net.IsServer && !IsClientSide(woundableEntity))
                    QueueDel(woundableEntity);

                if (_net.IsServer)
                    _body.GibBody(bodyPart.Body.Value); // More blood for the Blood Gods!
            }
            else
            {
                if (!Containers.TryGetContainingContainer(parentWoundableEntity, woundableEntity, out var container))
                    return;

                if (bodyPart.Body is not null
                    && TryComp<InventoryComponent>(bodyPart.Body, out var inventory) // Prevent error for non-humanoids
                    && _body.GetBodyPartCount(bodyPart.Body.Value, bodyPart.PartType) == 1
                    && _body.TryGetPartSlotContainerName(bodyPart.PartType, out var containerNames))
                {
                    foreach (var containerName in containerNames)
                    {
                        _inventory.DropSlotContents(bodyPart.Body.Value, containerName, inventory);
                    }
                }

                var bodyPartId = container.ID;
                if (bodyPart.PartType is BodyPartType.Hand or BodyPartType.Arm)
                {
                    // Prevent anomalous behaviour
                    _hands.TryDrop(bodyPart.Body!.Value, woundableEntity);
                }

                DropWoundableOrgans(woundableEntity, woundableComp);
                DestroyWoundableChildren(woundableEntity, woundableComp);

                foreach (var wound in GetWoundableWounds(woundableEntity, woundableComp))
                {
                    TransferWoundDamage(parentWoundableEntity, woundableEntity, wound);
                }

                if (TryInduceWound(parentWoundableEntity, "Blunt", 15f, out var woundEnt))
                {
                    _trauma.AddTrauma(
                        parentWoundableEntity,
                        (parentWoundableEntity, _woundableQuery.Comp(parentWoundableEntity)),
                        (woundEnt.Value.Owner, EnsureComp<TraumaInflicterComponent>(woundEnt.Value.Owner)),
                        TraumaType.Dismemberment,
                        15f);
                }

                foreach (var wound in GetWoundableWounds(parentWoundableEntity))
                {
                    if (!TryComp<BleedInflicterComponent>(wound, out var bleeds))
                        continue;

                    // Bleeding :3
                    bleeds.ScalingLimit += 6;
                }

                _body.DetachPart(parentWoundableEntity, bodyPartId.Remove(0, 15), woundableEntity);
                if (_net.IsServer && !IsClientSide(woundableEntity))
                    QueueDel(woundableEntity);
            }
        }
    }

    /// <summary>
    /// Amputates (not destroys) an entity's body part if conditions are met.
    /// </summary>
    /// <param name="parentWoundableEntity">Parent of the woundable entity. Yes.</param>
    /// <param name="woundableEntity">The entity containing the vulnerable body part</param>
    /// <param name="woundableComp">Woundable component of woundableEntity.</param>
    public void AmputateWoundable(EntityUid parentWoundableEntity, EntityUid woundableEntity, WoundableComponent? woundableComp = null)
    {
        if (!_woundableQuery.Resolve(woundableEntity, ref woundableComp))
            return;

        var bodyPart = Comp<BodyPartComponent>(parentWoundableEntity);
        if (!bodyPart.Body.HasValue)
            return;

        _audio.PlayPvs(woundableComp.WoundableDelimbedSound, bodyPart.Body.Value);

        foreach (var wound in GetWoundableWounds(woundableEntity, woundableComp))
        {
            TransferWoundDamage(parentWoundableEntity, woundableEntity, wound);
        }

        foreach (var wound in GetWoundableWounds(parentWoundableEntity))
        {
            if (!TryComp<BleedInflicterComponent>(wound, out var bleeds))
                continue;

            bleeds.ScalingLimit += 6;
        }

        AmputateWoundableSafely(parentWoundableEntity, woundableEntity);
        _throwing.TryThrow(woundableEntity, Random.NextAngle().ToWorldVec() * 7f, Random.Next(8, 24));
    }

    /// <summary>
    /// Does whatever AmputateWoundable does, but does it without pain and the other mess.
    /// </summary>
    /// <param name="parentWoundableEntity">Parent of the woundable entity. Yes.</param>
    /// <param name="woundableEntity">The entity containing the vulnerable body part</param>
    /// <param name="woundableComp">Woundable component of woundableEntity.</param>
    public void AmputateWoundableSafely(EntityUid parentWoundableEntity,
        EntityUid woundableEntity,
        WoundableComponent? woundableComp = null)
    {
        if (!_woundableQuery.Resolve(woundableEntity, ref woundableComp))
            return;

        var bodyPart = Comp<BodyPartComponent>(parentWoundableEntity);
        if (!bodyPart.Body.HasValue)
            return;

        if (!Containers.TryGetContainingContainer(parentWoundableEntity, woundableEntity, out var container))
            return;

        var bodyPartId = container.ID;
        woundableComp.WoundableSeverity = WoundableSeverity.Loss;

        if (TryComp<TargetingComponent>(bodyPart.Body.Value, out var targeting))
        {
            targeting.BodyStatus = GetWoundableStatesOnBodyPainFeels(bodyPart.Body.Value);
            Dirty(bodyPart.Body.Value, targeting);

            if (_net.IsServer)
                RaiseNetworkEvent(new TargetIntegrityChangeEvent(GetNetEntity(bodyPart.Body.Value)), bodyPart.Body.Value);
        }

        var childBodyPart = Comp<BodyPartComponent>(woundableEntity);
        if (TryComp<InventoryComponent>(bodyPart.Body, out var inventory)
            && _body.GetBodyPartCount(bodyPart.Body.Value, bodyPart.PartType) == 1
            && _body.TryGetPartSlotContainerName(childBodyPart.PartType, out var containerNames))
        {
            foreach (var containerName in containerNames)
            {
                _inventory.DropSlotContents(bodyPart.Body.Value, containerName, inventory);
            }
        }

        if (childBodyPart.PartType is BodyPartType.Hand or BodyPartType.Arm)
        {
            // Prevent anomalous behaviour
            _hands.TryDrop(bodyPart.Body!.Value, woundableEntity);
        }

        Dirty(woundableEntity, woundableComp);

        // Still does the funny popping, if the children are critted. for the funny :3
        DestroyWoundableChildren(woundableEntity, woundableComp);
        _body.DetachPart(parentWoundableEntity, bodyPartId.Remove(0, 15), woundableEntity);
    }

    #endregion

    #region Private API

    private void DropWoundableOrgans(EntityUid woundable, WoundableComponent? woundableComp)
    {
        if (!_woundableQuery.Resolve(woundable, ref woundableComp, false))
            return;

        foreach (var organ in _body.GetPartOrgans(woundable))
        {
            if (organ.Component.OrganSeverity == OrganSeverity.Normal)
            {
                // TODO: SFX for organs getting not destroyed, but thrown out
                _body.RemoveOrgan(organ.Id, organ.Component);
                _throwing.TryThrow(organ.Id, Random.NextAngle().ToWorldVec() * 7f, Random.Next(8, 24));
            }
            else
            {
                // Destroy it
                _trauma.TrySetOrganDamageModifier(
                    organ.Id,
                    organ.Component.OrganIntegrity * 100,
                    woundable,
                    WoundableDestroyalIdentifier,
                    organ.Component);
            }
        }
    }

    private void TransferWoundDamage(
        EntityUid parent,
        EntityUid severed,
        EntityUid wound,
        WoundableComponent? woundableComp = null,
        WoundComponent? woundComp = null)
    {
        if (!_woundableQuery.Resolve(parent, ref woundableComp, false)
            || !_woundQuery.Resolve(wound, ref woundComp, false))
            return;

        TryInduceWound(
            parent,
            woundComp.DamageType,
            woundComp.WoundSeverityPoint * _woundTransferPart,
            out _,
            woundableComp);

        var bodyPart = Comp<BodyPartComponent>(severed);
        foreach (var woundEnt in GetWoundableWounds(parent, woundableComp))
        {
            if (woundEnt.Comp.DamageType != woundComp.DamageType)
                continue;

            var tourniquetable = EnsureComp<TourniquetableComponent>(woundEnt);
            tourniquetable.SeveredSymmetry = bodyPart.Symmetry;
            tourniquetable.SeveredPartType = bodyPart.PartType;
        }
    }

    protected void InternalAddWoundableToParent(
        EntityUid parentEntity,
        EntityUid childEntity,
        WoundableComponent parentWoundable,
        WoundableComponent childWoundable)
    {
        parentWoundable.ChildWoundables.Add(childEntity);
        childWoundable.ParentWoundable = parentEntity;
        childWoundable.RootWoundable = parentWoundable.RootWoundable;

        FixWoundableRoots(childEntity, childWoundable);

        var woundableRoot = _woundableQuery.Comp(parentWoundable.RootWoundable);
        var woundableAttached = new WoundableAttachedEvent(parentEntity, parentWoundable);

        RaiseLocalEvent(childEntity, ref woundableAttached);

        foreach (var (woundId, wound) in GetAllWounds(childEntity, childWoundable))
        {
            var ev = new WoundAddedEvent(wound, parentWoundable, woundableRoot);
            RaiseLocalEvent(woundId, ref ev);

            var bodyPart = Comp<BodyPartComponent>(childEntity);
            if (bodyPart.Body.HasValue)
            {
                var ev2 = new WoundAddedOnBodyEvent((woundId, wound), parentWoundable, woundableRoot);
                RaiseLocalEvent(bodyPart.Body.Value, ref ev2);
            }
        }

        Dirty(childEntity, childWoundable);
    }

    protected void InternalRemoveWoundableFromParent(
        EntityUid parentEntity,
        EntityUid childEntity,
        WoundableComponent parentWoundable,
        WoundableComponent childWoundable)
    {
        if (TerminatingOrDeleted(childEntity) || TerminatingOrDeleted(parentEntity))
            return;

        parentWoundable.ChildWoundables.Remove(childEntity);
        childWoundable.ParentWoundable = null;
        childWoundable.RootWoundable = childEntity;

        FixWoundableRoots(childEntity, childWoundable);

        var oldWoundableRoot = _woundableQuery.Comp(parentWoundable.RootWoundable);
        var woundableDetached = new WoundableDetachedEvent(parentEntity, parentWoundable);

        RaiseLocalEvent(childEntity, ref woundableDetached);

        foreach (var (woundId, wound) in GetAllWounds(childEntity, childWoundable))
        {
            var ev = new WoundRemovedEvent(wound, childWoundable, oldWoundableRoot);
            RaiseLocalEvent(woundId, ref ev);

            var ev2 = new WoundRemovedEvent(wound, childWoundable, oldWoundableRoot);
            RaiseLocalEvent(childWoundable.RootWoundable, ref ev2);
        }

        Dirty(childEntity, childWoundable);
    }

    private void FixWoundableRoots(EntityUid targetEntity, WoundableComponent targetWoundable)
    {
        if (targetWoundable.ChildWoundables.Count == 0)
            return;

        foreach (var (childEntity, childWoundable) in GetAllWoundableChildren(targetEntity, targetWoundable))
        {
            childWoundable.RootWoundable = targetWoundable.RootWoundable;
            Dirty(childEntity, childWoundable);
        }

        Dirty(targetEntity, targetWoundable);
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

    private void DestroyWoundableChildren(EntityUid woundableEntity, WoundableComponent? woundableComp = null)
    {
        if (!_woundableQuery.Resolve(woundableEntity, ref woundableComp, false))
            return;

        foreach (var child in woundableComp.ChildWoundables)
        {
            var childWoundable = _woundableQuery.Comp(child);
            if (childWoundable.WoundableSeverity is WoundableSeverity.Critical)
            {
                DestroyWoundable(woundableEntity, child, childWoundable);
                continue;
            }

            AmputateWoundable(woundableEntity, child, childWoundable);
        }
    }

    public Dictionary<TargetBodyPart, WoundableSeverity> GetWoundableStatesOnBody(EntityUid body)
    {
        var result = new Dictionary<TargetBodyPart, WoundableSeverity>();

        foreach (var part in SharedTargetingSystem.GetValidParts())
        {
            result[part] = WoundableSeverity.Loss;
        }

        foreach (var (id, bodyPart) in _body.GetBodyChildren(body))
        {
            var target = _body.GetTargetBodyPart(bodyPart);
            if (target == null)
                continue;

            if (!_woundableQuery.TryComp(id, out var woundable))
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

        foreach (var (id, bodyPart) in _body.GetBodyChildren(body))
        {
            var target = _body.GetTargetBodyPart(bodyPart);
            if (target == null)
                continue;

            if (!_woundableQuery.TryComp(id, out var woundable) || !TryComp<NerveComponent>(id, out var nerve))
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
        return _woundableQuery.Resolve(woundableEntity, ref woundable, false) && woundable.RootWoundable == woundableEntity;
    }

    /// <summary>
    /// Retrieves all wounds associated with a specified entity.
    /// </summary>
    /// <param name="targetEntity">The UID of the target entity.</param>
    /// <param name="targetWoundable">Optional: The WoundableComponent of the target entity.</param>
    /// <returns>An enumerable collection of tuples containing EntityUid and WoundComponent pairs.</returns>
    public IEnumerable<Entity<WoundComponent>> GetAllWounds(EntityUid targetEntity,
        WoundableComponent? targetWoundable = null)
    {
        if (!_woundableQuery.Resolve(targetEntity, ref targetWoundable, false))
            yield break;

        foreach (var (_, childWoundable) in GetAllWoundableChildren(targetEntity, targetWoundable))
        {
            if (childWoundable.Wounds == null)
                continue;
            foreach (var woundEntity in childWoundable.Wounds.ContainedEntities)
            {
                yield return (woundEntity, _woundQuery.Comp(woundEntity));
            }
        }
    }

    /// <summary>
    /// Gets all woundable children of a specified woundable
    /// </summary>
    /// <param name="targetEntity">Owner of the woundable</param>
    /// <param name="targetWoundable"></param>
    /// <returns>Enumerable to the found children</returns>
    public IEnumerable<Entity<WoundableComponent>> GetAllWoundableChildren(EntityUid targetEntity,
        WoundableComponent? targetWoundable = null)
    {
        if (!_woundableQuery.Resolve(targetEntity, ref targetWoundable, false))
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
    /// Parents a woundable to another
    /// </summary>
    /// <param name="parentEntity">Owner of the new parent</param>
    /// <param name="childEntity">Owner of the woundable we want to attach</param>
    /// <param name="parentWoundable">The new parent woundable component</param>
    /// <param name="childWoundable">The woundable we are attaching</param>
    /// <returns>true if successful</returns>
    public bool AddWoundableToParent(
        EntityUid parentEntity,
        EntityUid childEntity,
        WoundableComponent? parentWoundable = null,
        WoundableComponent? childWoundable = null)
    {
        if (!_woundableQuery.Resolve(parentEntity, ref parentWoundable, false)
            || !_woundableQuery.Resolve(childEntity, ref childWoundable, false) || childWoundable.ParentWoundable == null)
            return false;

        InternalAddWoundableToParent(parentEntity, childEntity, parentWoundable, childWoundable);
        return true;
    }

    /// <summary>
    /// Removes a woundable from its parent (if present)
    /// </summary>
    /// <param name="parentEntity">Owner of the parent woundable</param>
    /// <param name="childEntity">Owner of the child woundable</param>
    /// <param name="parentWoundable"></param>
    /// <param name="childWoundable"></param>
    /// <returns>true if successful</returns>
    public bool RemoveWoundableFromParent(
        EntityUid parentEntity,
        EntityUid childEntity,
        WoundableComponent? parentWoundable = null,
        WoundableComponent? childWoundable = null)
    {
        if (!_woundableQuery.Resolve(parentEntity, ref parentWoundable, false)
            || !_woundableQuery.Resolve(childEntity, ref childWoundable, false) || childWoundable.ParentWoundable == null)
            return false;

        InternalRemoveWoundableFromParent(parentEntity, childEntity, parentWoundable, childWoundable);
        return true;
    }


    /// <summary>
    /// Finds all children of a specified woundable that have a specific component
    /// </summary>
    /// <param name="targetEntity"></param>
    /// <param name="targetWoundable"></param>
    /// <typeparam name="T">the type of the component we want to find</typeparam>
    /// <returns>Enumerable to the found children</returns>
    public IEnumerable<Entity<WoundableComponent, T>> GetAllWoundableChildrenWithComp<T>(EntityUid targetEntity,
        WoundableComponent? targetWoundable = null) where T: Component, new()
    {
        if (!_woundableQuery.Resolve(targetEntity, ref targetWoundable, false))
            yield break;

        foreach (var childEntity in targetWoundable.ChildWoundables)
        {
            if (!_woundableQuery.TryComp(childEntity, out WoundableComponent? childWoundable))
                continue;

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
    /// Get the wounds present on a specific woundable
    /// </summary>
    /// <param name="targetEntity">Entity that owns the woundable</param>
    /// <param name="targetWoundable">Woundable component</param>
    /// <returns>An enumerable pointing to one of the found wounds</returns>
    public IEnumerable<Entity<WoundComponent>> GetWoundableWounds(EntityUid targetEntity,
        WoundableComponent? targetWoundable = null)
    {
        if (!_woundableQuery.Resolve(targetEntity, ref targetWoundable, false)
            || targetWoundable.Wounds == null || targetWoundable.Wounds.Count == 0)
            yield break;

        foreach (var woundEntity in targetWoundable.Wounds.ContainedEntities.ToList())
        {
            yield return (woundEntity, _woundQuery.Comp(woundEntity));
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
        if (!_woundableQuery.Resolve(targetEntity, ref targetWoundable, false)
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
        if (!_woundableQuery.Resolve(targetEntity, ref targetWoundable, false)
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
