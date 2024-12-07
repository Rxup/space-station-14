using System.Linq;
using System.Numerics;
using Content.Shared.Backmen.Surgery.Body.Events;
using Content.Shared.Backmen.Surgery.Traumas.Components;
using Content.Shared.Backmen.Surgery.Wounds.Components;
using Content.Shared.Body.Part;
using Content.Shared.FixedPoint;
using Content.Shared.Humanoid;
using Content.Shared.Inventory;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Shared.Backmen.Surgery.Wounds.Systems;

public partial class WoundSystem
{
    private const string WoundContainerId = "Wounds";
    private const string BoneContainerId = "Bone";

    private const string BoneEntityId = "Bone";

    private const float ScarChance = 0.10f;

    private void InitWounding()
    {
        SubscribeLocalEvent<WoundableComponent, ComponentInit>(OnWoundableInit);

        SubscribeLocalEvent<WoundableComponent, EntInsertedIntoContainerMessage>(OnWoundableInserted);
        SubscribeLocalEvent<WoundableComponent, EntRemovedFromContainerMessage>(OnWoundableRemoved);

        SubscribeLocalEvent<WoundComponent, EntGotInsertedIntoContainerMessage>(OnWoundInserted);
        SubscribeLocalEvent<WoundComponent, EntGotRemovedFromContainerMessage>(OnWoundRemoved);

        SubscribeLocalEvent<WoundComponent, WoundSeverityChangedEvent>(OnWoundSeverityChanged);
        SubscribeLocalEvent<WoundableComponent, WoundableSeverityChangedEvent>(OnWoundableSeverityChanged);

        SubscribeLocalEvent<WoundableComponent, BodyPartRemovedEvent>((_, _, args) => UpdateWoundableStates(args.Part.Comp.Body!.Value));
        SubscribeLocalEvent<WoundableComponent, BodyPartAddedEvent>((_, _, args) => UpdateWoundableStates(args.Part.Comp.Body!.Value));

        SubscribeNetworkEvent<OnWoundableLossDeleteMessage>((msg, _) => OnWoundableDelete(msg));
    }

    #region Event Handling

    private void OnWoundableDelete(OnWoundableLossDeleteMessage message)
    {
        if (TerminatingOrDeleted(GetEntity(message.Woundable)) || _net.IsClient)
            return;

        QueueDel(GetEntity(message.Woundable));
    }

    private void OnWoundableInit(EntityUid uid, WoundableComponent comp, ComponentInit componentInit)
    {
        // Set root to itself.
        comp.RootWoundable = uid;

        // Create container for wounds.
        comp.Wounds = _container.EnsureContainer<Container>(uid, WoundContainerId);
        comp.Bone = _container.EnsureContainer<Container>(uid, BoneContainerId);

        InsertBoneIntoWoundable(uid, comp);
    }

    private void OnWoundInserted(EntityUid uid, WoundComponent comp, EntGotInsertedIntoContainerMessage args)
    {
        var parentWoundable = Comp<WoundableComponent>(comp.Parent);
        var woundableRoot = Comp<WoundableComponent>(parentWoundable.RootWoundable);

        var ev = new WoundAddedEvent(uid, comp, parentWoundable, woundableRoot);
        RaiseLocalEvent(args.Entity, ref ev);

        var ev2 = new WoundAddedEvent(uid, comp, parentWoundable, woundableRoot);
        RaiseLocalEvent(parentWoundable.RootWoundable, ref ev2, true);
    }

    private void OnWoundRemoved(EntityUid woundableEntity, WoundComponent wound, EntGotRemovedFromContainerMessage args)
    {
        if (wound.Parent == EntityUid.Invalid)
            return;

        var oldParentWoundable = Comp<WoundableComponent>(wound.Parent);
        var oldWoundableRoot = Comp<WoundableComponent>(oldParentWoundable.RootWoundable);

        wound.Parent = EntityUid.Invalid;

        var ev = new WoundRemovedEvent(args.Entity, wound, oldParentWoundable, oldWoundableRoot);
        RaiseLocalEvent(args.Entity, ref ev);

        var ev2 = new WoundRemovedEvent(args.Entity, wound, oldParentWoundable, oldWoundableRoot);
        RaiseLocalEvent(oldParentWoundable.RootWoundable, ref ev2, true);

        if (_net.IsServer && !TerminatingOrDeleted(woundableEntity))
            Del(woundableEntity);
    }

    private void OnWoundableInserted(EntityUid parentEntity, WoundableComponent parentWoundable, EntInsertedIntoContainerMessage args)
    {
        if (_net.IsClient || !TryComp<WoundableComponent>(args.Entity, out var childWoundable))
            return;

        InternalAddWoundableToParent(parentEntity, args.Entity, parentWoundable, childWoundable);
    }

    private void OnWoundableRemoved(EntityUid parentEntity, WoundableComponent parentWoundable, EntRemovedFromContainerMessage args)
    {
        if (_net.IsClient || !TryComp<WoundableComponent>(args.Entity, out var childWoundable))
            return;

        InternalRemoveWoundableFromParent(parentEntity, args.Entity, parentWoundable, childWoundable);
    }

    private void OnWoundableSeverityChanged(EntityUid uid, WoundableComponent component, WoundableSeverityChangedEvent args)
    {
        if (args.NewSeverity != WoundableSeverity.Loss || TerminatingOrDeleted(uid))
            return;

        if (IsWoundableRoot(uid, component))
        {
            DestroyWoundable(uid, uid, component);
            // We can call DestroyWoundable instead of ProcessBodyPartLoss, because body will be gibbed, and we may not process body part loss.
        }
        else
        {
            if (component.ParentWoundable == null)
                return;

            ProcessBodyPartLoss(uid, component.ParentWoundable.Value, component);
        }
    }

    private void ProcessBodyPartLoss(EntityUid uid, EntityUid parentUid, WoundableComponent component)
    {
        var bodyPart = Comp<BodyPartComponent>(uid);
        if (bodyPart.Body == null)
            return;

        //todo: consciousness port
        //_consciousness.ForcePassout(bodyPart.Body.Value, 7);

        foreach (var wound in component.Wounds!.ContainedEntities)
        {
            Comp<WoundComponent>(wound).CanBeHealed = false;

            TransferWoundDamage(parentUid, wound, Comp<WoundComponent>(wound));
        }

        DestroyWoundable(parentUid, uid, component);
    }

    private void OnWoundSeverityChanged(EntityUid wound, WoundComponent woundComponent, WoundSeverityChangedEvent args)
    {
        if (args.NewSeverity != WoundSeverity.Healed || _net.IsClient)
            return;

        TryMakeScar(woundComponent);
        RemoveWound(wound);
    }

    #endregion

    #region Public API

    /// <summary>
    /// Opens a new wound on a requested woundable.
    /// </summary>
    /// <param name="uid">UID of the woundable (body part).</param>
    /// <param name="woundProtoId">Wound prototype.</param>
    /// <param name="severity">Severity for wound to apply.</param>
    /// <param name="damageGroup">Damage group.</param>
    /// <param name="woundable">Woundable component.</param>
    public bool TryCreateWound(
         EntityUid uid,
         string woundProtoId,
         FixedPoint2 severity,
         string damageGroup,
         WoundableComponent? woundable = null)
    {
        if (!Resolve(uid, ref woundable) || !_net.IsServer)
            return false;

        if (woundable.Wounds == null)
            return false;

        if (!IsWoundPrototypeValid(woundProtoId))
            return false;

        var wound = Spawn(woundProtoId);

        return AddWound(uid, wound, severity, damageGroup);
    }

    /// <summary>
    /// Continues wound with specific type, if there's any. Adds severity to it basically.
    /// </summary>
    /// <param name="id">Wound entity's ID.</param>
    /// <param name="severity">Severity to apply.</param>
    /// <param name="woundable">Woundable for wound to add.</param>
    /// <returns>Returns true, if wound was continued.</returns>
    public bool TryContinueWound(string id, FixedPoint2 severity, WoundableComponent woundable)
    {
        if (!IsWoundPrototypeValid(id) || _net.IsClient)
            return false;

        var proto = _prototype.Index(id);
        foreach (var wound in woundable.Wounds!.ContainedEntities)
        {
            if (proto.ID != MetaData(wound).EntityPrototype!.ID)
                continue;

            ApplyWoundSeverity(wound, severity);

            return true;
        }

        return false;
    }

    /// <summary>
    /// Tries to create a scar on a woundable entity. Takes scar proto from WoundComponent.
    /// </summary>
    /// <param name="woundComponent">The WoundComponent representing a specific wound.</param>
    public void TryMakeScar(WoundComponent woundComponent)
    {
        if (_net.IsServer && _random.Prob(ScarChance) && woundComponent is { ScarWound: not null, IsScar: false })
        {
            TryCreateWound(woundComponent.Parent, woundComponent.ScarWound, 1, woundComponent.DamageGroup);
        }
    }

    /// <summary>
    /// Sets severity of a wound.
    /// </summary>
    /// <param name="uid">UID of the wound.</param>
    /// <param name="severity">Severity to set.</param>
    /// <param name="wound">Wound to which severity is applied.</param>
    public void SetWoundSeverity(EntityUid uid, FixedPoint2 severity, WoundComponent? wound = null)
    {
        if (!Resolve(uid, ref wound) || _net.IsClient)
            return;

        var old = wound.WoundSeverityPoint;

        wound.WoundSeverityPoint = ApplyModifiersToSeverity(wound, severity);

        var ev = new WoundSeverityPointChangedEvent(uid, wound, old, severity);
        RaiseLocalEvent(uid, ref ev);

        CheckSeverityThresholds(uid, wound);
        Dirty(uid, wound);

        //todo: trauma port
        //_trauma.TryApplyTrauma(wound.Parent, severity);

        if (!TryComp<WoundableComponent>(wound.Parent, out var woundable))
            return;

        UpdateWoundableIntegrity(wound.Parent, woundable);
        Dirty(uid, wound);
    }

    /// <summary>
    /// Applies severity to a wound
    /// </summary>
    /// <param name="uid">UID of the wound.</param>
    /// <param name="severity">Severity to add.</param>
    /// <param name="wound">Wound to which severity is applied.</param>
    /// <param name="isHealing">Name speaks for this.</param>
    public void ApplyWoundSeverity(EntityUid uid, FixedPoint2 severity, WoundComponent? wound = null, bool isHealing = false)
    {
        if (!Resolve(uid, ref wound) || _net.IsClient)
            return;

        var old = wound.WoundSeverityPoint;

        wound.WoundSeverityPoint += isHealing ? severity : ApplyModifiersToSeverity(wound, severity);
        // Apply modifiers, actually we've got "raw" severity up here.
        // Healing modifiers are applied, so doing this again isn't needed.

        var ev = new WoundSeverityPointChangedEvent(uid, wound, old, severity);
        RaiseLocalEvent(uid, ref ev);

        CheckSeverityThresholds(uid, wound);
        Dirty(uid, wound);

        //todo: trauma port
        //_trauma.TryApplyTrauma(wound.Parent, severity);

        if (!TryComp<WoundableComponent>(wound.Parent, out var woundable))
            return;

        UpdateWoundableIntegrity(wound.Parent, woundable);
        Dirty(uid, wound);
    }

    /// <summary>
    /// Applies severity multiplier to a wound.
    /// </summary>
    /// <param name="uid">UID of the wound.</param>
    /// <param name="severity">Severity multiplier.</param>
    /// <param name="identifier">Identifier for multiplier.</param>
    /// <param name="wound">Wound to which severity multiplier is applied.</param>
    public bool ApplyWoundSeverityMultiplier(EntityUid uid, FixedPoint2 severity, string identifier, WoundComponent? wound = null)
    {
        if (!Resolve(uid, ref wound) || _net.IsClient)
            return false;

        wound.SeverityMultipliers.Add((uid, wound), new WoundSeverityMultiplier(severity, identifier));

        ApplyModifiersToSeverity(wound, wound.WoundSeverityPoint);
        CheckSeverityThresholds(uid, wound);
        Dirty(uid, wound);

        if (!TryComp<WoundableComponent>(wound.Parent, out var woundable))
            return false;

        UpdateWoundableIntegrity(wound.Parent, woundable);
        Dirty(uid, wound);

        return true;
    }

    /// <summary>
    /// Applies severity multiplier to a wound.
    /// </summary>
    /// <param name="uid">UID of the wound.</param>
    /// <param name="identifier">Identifier for multiplier.</param>
    /// <param name="wound">Wound to which severity multiplier is applied.</param>
    public bool RemoveWoundSeverityMultiplier(EntityUid uid, string identifier, WoundComponent? wound = null)
    {
        if (!Resolve(uid, ref wound) || _net.IsClient)
            return false;
        if (!wound.SeverityMultipliers.Remove((uid, wound), out _))
            return false;

        ApplyModifiersToSeverity(wound, wound.WoundSeverityPoint);
        CheckSeverityThresholds(uid, wound);
        Dirty(uid, wound);

        if (!TryComp<WoundableComponent>(wound.Parent, out var woundable))
            return false;

        UpdateWoundableIntegrity(wound.Parent, woundable);
        Dirty(wound.Parent, woundable);

        return true;
    }

    #endregion

    #region Private API

    private void InsertBoneIntoWoundable(EntityUid uid, WoundableComponent? comp = null)
    {
        if (!Resolve(uid, ref comp, false) || _net.IsClient)
            return;

        var bone = Spawn(BoneEntityId);

        //todo: bones and traumas
        if (!TryComp<BoneComponent>(bone, out var boneComp))
            return;

        boneComp.BoneWoundable = uid;

        _transform.SetParent(bone, uid);
        _container.Insert(bone, comp.Bone!);
    }

    private void TransferWoundDamage(EntityUid parent, EntityUid wound, WoundComponent woundComp, WoundableComponent? woundableComp = null)
    {
        if (!Resolve(parent, ref woundableComp, false) || _net.IsClient)
            return;

        if (!TryContinueWound(MetaData(wound).EntityPrototype!.ID, woundComp.WoundSeverityPoint / 4, woundableComp))
        {
            TryCreateWound(
                parent,
                MetaData(wound).EntityPrototype!.ID,
                woundComp.WoundSeverityPoint / 4,
                woundComp.DamageGroup,
                woundableComp);
        }
    }

    private void UpdateWoundableIntegrity(EntityUid uid, WoundableComponent woundable)
    {
        woundable.WoundableIntegrity =
            woundable.Wounds!.ContainedEntities.Aggregate((FixedPoint2) 0, (current, wound) => current + Comp<WoundComponent>(wound).WoundSeverityPoint);

        CheckWoundableSeverityThresholds(uid, woundable);

        var ev = new WoundableIntegrityChangedEvent(uid, woundable.WoundableIntegrity);
        RaiseLocalEvent(uid, ref ev);
    }

    protected bool AddWound(
        EntityUid target,
        EntityUid wound,
        FixedPoint2 woundSeverity,
        string damageGroup,
        WoundableComponent? woundableComponent = null,
        WoundComponent? woundComponent = null)
    {
        if (!Resolve(target, ref woundableComponent)
            || !Resolve(wound, ref woundComponent)
            || woundableComponent.Wounds!.Contains(wound))
            return false;

        _transform.SetParent(wound, target);

        woundComponent.Parent = target;
        woundComponent.DamageGroup = damageGroup;

        SetWoundSeverity(wound, woundSeverity);

        Dirty(wound, woundComponent);
        Dirty(target, woundableComponent);

        if (!_container.Insert(wound, woundableComponent.Wounds))
            return false;

        var woundMeta = MetaData(wound);
        var targetMeta = MetaData(target);

        _sawmill.Info($"Wound: {woundMeta.EntityPrototype!.ID}({wound}) created on {targetMeta.EntityPrototype!.ID}({target})");

        return true;
    }

    protected bool RemoveWound(EntityUid woundEntity, WoundComponent? wound = null)
    {
        if (!Resolve(woundEntity, ref wound)
            || !TryComp(wound.Parent, out WoundableComponent? woundable)
            || woundable.Wounds == null)
            return false;

        return _container.Remove(woundEntity, woundable.Wounds);
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

        var woundableRoot = Comp<WoundableComponent>(parentWoundable.RootWoundable);
        var woundableAttached= new WoundableAttachedEvent(parentEntity, parentWoundable);

        RaiseLocalEvent(childEntity, ref woundableAttached, true);

        foreach (var (woundId, wound) in GetAllWounds(childEntity, childWoundable))
        {
            var ev = new WoundAddedEvent(woundId, wound, childWoundable, woundableRoot);
            RaiseLocalEvent(woundId, ref ev);

            var ev2 = new WoundAddedEvent(woundId, wound, childWoundable, woundableRoot);
            RaiseLocalEvent(childWoundable.RootWoundable, ref ev2, true);
        }

        Dirty(childEntity, childWoundable);
    }

    protected void InternalRemoveWoundableFromParent(
        EntityUid parentEntity,
        EntityUid childEntity,
        WoundableComponent parentWoundable,
        WoundableComponent childWoundable)
    {
        parentWoundable.ChildWoundables.Remove(childEntity);
        childWoundable.ParentWoundable = null;
        childWoundable.RootWoundable = childEntity;

        FixWoundableRoots(childEntity, childWoundable);

        var oldWoundableRoot = Comp<WoundableComponent>(parentWoundable.RootWoundable);
        var woundableDetached = new WoundableDetachedEvent(parentEntity, parentWoundable);

        RaiseLocalEvent(childEntity, ref woundableDetached, true);

        foreach (var (woundId, wound) in GetAllWounds(childEntity, childWoundable))
        {
            var ev = new WoundRemovedEvent(woundId, wound, childWoundable, oldWoundableRoot);
            RaiseLocalEvent(woundId, ref ev);

            var ev2 = new WoundRemovedEvent(woundId, wound, childWoundable, oldWoundableRoot);
            RaiseLocalEvent(childWoundable.RootWoundable, ref ev2, true);
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

    private FixedPoint2 ApplyModifiersToSeverity(WoundComponent wound, FixedPoint2 severityChange)
    {
        if (wound.SeverityMultipliers.Count == 0)
            return severityChange;

        var severityMultiplier = wound.SeverityMultipliers.Sum(multiplier => (float) multiplier.Value.Change);
        return severityChange * severityMultiplier / wound.SeverityMultipliers.Count;
    }

    private void CheckSeverityThresholds(EntityUid wound, WoundComponent woundComponent)
    {
        var nearestSeverity = woundComponent.WoundSeverity;

        foreach (var (severity, value) in _woundThresholds.OrderByDescending(kv => kv.Value))
        {
            if (woundComponent.WoundSeverityPoint < value)
                continue;

            if (severity == WoundSeverity.Healed && woundComponent.WoundSeverityPoint > 0)
                continue;

            nearestSeverity = severity;
            break;
        }

        if (nearestSeverity != woundComponent.WoundSeverity)
        {
            var ev = new WoundSeverityChangedEvent(wound, nearestSeverity);
            RaiseLocalEvent(wound, ref ev, true);
        }
        woundComponent.WoundSeverity = nearestSeverity;

        Dirty(wound, woundComponent);
    }

    private void CheckWoundableSeverityThresholds(EntityUid woundable, WoundableComponent woundableComp)
    {
        var nearestSeverity = woundableComp.WoundableSeverity;

        foreach (var (severity, value) in _woundableThresholds.OrderByDescending(kv => kv.Value))
        {
            if (woundableComp.WoundableIntegrity < value)
                continue;

            nearestSeverity = severity;
            break;
        }

        if (woundableComp.ForceLoss)
            nearestSeverity = WoundableSeverity.Loss;

        if (nearestSeverity != woundableComp.WoundableSeverity)
        {
            var ev = new WoundableSeverityChangedEvent(woundable, nearestSeverity);
            RaiseLocalEvent(woundable, ref ev, true);
        }
        woundableComp.WoundableSeverity = nearestSeverity;

        _appearance.SetData(woundable, WoundableVisualizerKeys.Severity, woundableComp.WoundableSeverity);
        Dirty(woundable, woundableComp);
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
    private bool IsWoundPrototypeValid(string protoId)
    {
        return _prototype.TryIndex<EntityPrototype>(protoId, out var woundPrototype)
               && woundPrototype.TryGetComponent<WoundComponent>(out _, _factory);
    }

    /// <summary>
    /// Destroys an entity's body part if conditions are met.
    /// </summary>
    /// <param name="parentWoundableEntity">Parent of the woundable entity. Yes.</param>
    /// <param name="woundableEntity">The entity containing the vulnerable body part</param>
    /// <param name="woundableComp">Woundable component of woundableEntity.</param>
    private void DestroyWoundable(EntityUid parentWoundableEntity, EntityUid woundableEntity, WoundableComponent woundableComp)
    {
        var bodyPart = Comp<BodyPartComponent>(woundableEntity);
        if (bodyPart.Body == null)
            return;

        var key = bodyPart.ToHumanoidLayers();
        if (key == null)
            return;

        woundableComp.ForceLoss = true;

        _body.DropSlotContents(new Entity<BodyPartComponent>(woundableEntity, Comp<BodyPartComponent>(woundableEntity)));
        if (IsWoundableRoot(woundableEntity, woundableComp))
        {
            DestroyWoundableChildren(woundableEntity, woundableComp);
            _body.GibBody(bodyPart.Body.Value);

            QueueDel(woundableEntity); // More blood for the blood God!
        }
        else
        {
            if (!_container.TryGetContainingContainer(parentWoundableEntity, woundableEntity, out var container))
                return;

            var bodyPartId = container.ID;

            DestroyWoundableChildren(woundableEntity, woundableComp);

            RaiseNetworkEvent(new OnWoundableLossDeleteMessage(GetNetEntity(woundableEntity), GetNetEntity(bodyPart.Body.Value), key.Value));
            _body.DetachPart(parentWoundableEntity, bodyPartId.Remove(0, 15), woundableEntity);

            _transform.SetWorldPosition(woundableEntity, new Vector2(0)); // Entity is detached and then removed. We don't want for it to be seen.
        }
    }

    /// <summary>
    /// Amputates (not destroys) an entity's body part if conditions are met.
    /// </summary>
    /// <param name="parentWoundableEntity">Parent of the woundable entity. Yes.</param>
    /// <param name="woundableEntity">The entity containing the vulnerable body part</param>
    /// <param name="woundableComp">Woundable component of woundableEntity.</param>
    private void AmputateWoundable(EntityUid parentWoundableEntity, EntityUid woundableEntity, WoundableComponent woundableComp)
    {
        var bodyPart = Comp<BodyPartComponent>(woundableEntity);
        if (bodyPart.Body == null)
            return;

        if (!_container.TryGetContainingContainer(parentWoundableEntity, woundableEntity, out var container))
            return;

        foreach (var wound in woundableComp.Wounds!.ContainedEntities)
        {
            Comp<WoundComponent>(wound).CanBeHealed = false;
        }

        var bodyPartId = container.ID;

        woundableComp.ForceLoss = true;

        DestroyWoundableChildren(woundableEntity, woundableComp);
        _body.DetachPart(parentWoundableEntity, bodyPartId.Remove(0, 15), woundableEntity);
    }

    private bool DestroyWoundableChildren(EntityUid woundableEntity, WoundableComponent? woundableComp = null)
    {
        if (!Resolve(woundableEntity, ref woundableComp))
            return false;

        foreach (var child in woundableComp.ChildWoundables)
        {
            if (woundableComp.WoundableSeverity is WoundableSeverity.Loss or WoundableSeverity.Critical)
            {
                DestroyWoundable(woundableEntity, child, Comp<WoundableComponent>(child));
                continue;
            }

            AmputateWoundable(woundableEntity, child, Comp<WoundableComponent>(child));
        }

        return true;
    }

    private void UpdateWoundableStates(EntityUid body)
    {
        foreach (var bodyPart in _body.GetBodyChildren(body))
        {
            _appearance.SetData(bodyPart.Id, WoundableVisualizerKeys.Update, true);
        }
    }

    /// <summary>
    /// Check if this woundable is root
    /// </summary>
    /// <param name="woundableEntity">Owner of the woundable</param>
    /// <param name="woundable">woundable component</param>
    /// <returns>true if the woundable is the root of the hierarchy</returns>
    public bool IsWoundableRoot(EntityUid woundableEntity, WoundableComponent? woundable = null)
    {
        return Resolve(woundableEntity, ref woundable) && woundable.RootWoundable == woundableEntity;
    }

    /// <summary>
    /// Retrieves all wounds associated with a specified entity.
    /// </summary>
    /// <param name="targetEntity">The UID of the target entity.</param>
    /// <param name="targetWoundable">Optional: The WoundableComponent of the target entity.</param>
    /// <returns>An enumerable collection of tuples containing EntityUid and WoundComponent pairs.</returns>
    public IEnumerable<(EntityUid, WoundComponent)> GetAllWounds(EntityUid targetEntity,
        WoundableComponent? targetWoundable = null)
    {
        if (!Resolve(targetEntity, ref targetWoundable) || targetWoundable.Wounds!.Count == 0)
            yield break;

        foreach (var (_, childWoundable) in GetAllWoundableChildren(targetEntity, targetWoundable))
        {
            foreach (var woundEntity in childWoundable.Wounds!.ContainedEntities)
            {
                yield return (woundEntity, Comp<WoundComponent>(woundEntity));
            }
        }
    }

    /// <summary>
    /// Gets all woundable children of a specified woundable
    /// </summary>
    /// <param name="targetEntity">Owner of the woundable</param>
    /// <param name="targetWoundable"></param>
    /// <returns>Enumerable to the found children</returns>
    public IEnumerable<(EntityUid, WoundableComponent)> GetAllWoundableChildren(EntityUid targetEntity,
        WoundableComponent? targetWoundable = null)
    {
        if (!Resolve(targetEntity, ref targetWoundable))
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
        if (!Resolve(parentEntity, ref parentWoundable) || _net.IsClient
            || !Resolve(childEntity, ref childWoundable) || childWoundable.ParentWoundable == null)
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
        if (!Resolve(parentEntity, ref parentWoundable) || _net.IsClient
            || !Resolve(childEntity, ref childWoundable) || childWoundable.ParentWoundable == null)
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
    public IEnumerable<(EntityUid, WoundableComponent, T)> GetAllWoundableChildrenWithComp<T>(EntityUid targetEntity,
        WoundableComponent? targetWoundable = null) where T: Component, new()
    {
        if (!Resolve(targetEntity, ref targetWoundable))
            yield break;

        foreach (var childEntity in targetWoundable.ChildWoundables)
        {
            if (!TryComp(childEntity, out WoundableComponent? childWoundable))
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
    public IEnumerable<(EntityUid, WoundComponent)> GetWoundableWounds(EntityUid targetEntity,
        WoundableComponent? targetWoundable = null)
    {
        if (!Resolve(targetEntity, ref targetWoundable) || targetWoundable.Wounds!.Count == 0)
            yield break;

        foreach (var woundEntity in targetWoundable.Wounds!.ContainedEntities)
        {
            yield return (woundEntity, Comp<WoundComponent>(woundEntity));
        }
    }

    #endregion
}
