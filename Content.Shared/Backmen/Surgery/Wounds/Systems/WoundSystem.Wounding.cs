using System.Linq;
using Content.Shared.Backmen.Surgery.Traumas;
using Content.Shared.Backmen.Surgery.Traumas.Components;
using Content.Shared.Backmen.Surgery.Wounds.Components;
using Content.Shared.Backmen.Targeting;
using Content.Shared.Body.Part;
using Content.Shared.FixedPoint;
using Content.Shared.Humanoid;
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

    private const float WoundMaxSeverity = 200f;

    private void InitWounding()
    {
        SubscribeLocalEvent<WoundableComponent, ComponentInit>(OnWoundableInit);

        SubscribeLocalEvent<WoundableComponent, EntInsertedIntoContainerMessage>(OnWoundableInserted);
        SubscribeLocalEvent<WoundableComponent, EntRemovedFromContainerMessage>(OnWoundableRemoved);

        SubscribeLocalEvent<WoundComponent, EntGotInsertedIntoContainerMessage>(OnWoundInserted);
        SubscribeLocalEvent<WoundComponent, EntGotRemovedFromContainerMessage>(OnWoundRemoved);

        SubscribeLocalEvent<WoundComponent, WoundSeverityChangedEvent>(OnWoundSeverityChanged);
        SubscribeLocalEvent<WoundableComponent, WoundableSeverityChangedEvent>(OnWoundableSeverityChanged);
    }

    #region Event Handling

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
    /// <param name="traumaList">Trauma list to set, if you wanna</param>
    public bool TryCreateWound(
         EntityUid uid,
         string woundProtoId,
         FixedPoint2 severity,
         string damageGroup,
         WoundableComponent? woundable = null,
         IEnumerable<TraumaType>? traumaList = null)
    {
        if (!IsWoundPrototypeValid(woundProtoId) || _net.IsClient)
            return false;

        if (!Resolve(uid, ref woundable))
            return false;

        var wound = Spawn(woundProtoId);

        return AddWound(uid, wound, severity, damageGroup, traumaList: traumaList);
    }

    /// <summary>
    /// Continues wound with specific type, if there's any. Adds severity to it basically.
    /// </summary>
    /// <param name="uid">Woundable entity's UID.</param>
    /// <param name="id">Wound entity's ID.</param>
    /// <param name="severity">Severity to apply.</param>
    /// <param name="woundable">Woundable for wound to add.</param>
    /// <returns>Returns true, if wound was continued.</returns>
    public bool TryContinueWound(EntityUid uid, string id, FixedPoint2 severity, WoundableComponent? woundable = null)
    {
        if (!IsWoundPrototypeValid(id) || _net.IsClient)
            return false;

        if (!Resolve(uid, ref woundable, false))
            return false;

        var traumas = _trauma.RandomTraumaChance(uid, severity, woundable).ToList();
        var openNewWound = true;

        var proto = _prototype.Index(id);
        foreach (var wound in woundable.Wounds!.ContainedEntities)
        {
            if (proto.ID != MetaData(wound).EntityPrototype!.ID)
                continue;

            var comp = Comp<WoundComponent>(wound);
            if (comp is { CanApplyTrauma: true, WoundType: WoundType.Internal })
            {
                openNewWound = false;
                ApplyWoundSeverity(wound, severity, comp, traumaList: traumas);

                continue;
            }

            if (openNewWound)
            {
                TryCreateWound(uid, proto.ID, severity, comp.DamageGroup!, woundable, traumaList: traumas);
            }

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
        if (_net.IsServer && _random.Prob(ScarChance) && woundComponent is { ScarWound: not null, DamageGroup: not null, IsScar: false })
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
        if (!Resolve(uid, ref wound) || !_net.IsServer)
            return;

        var old = wound.WoundSeverityPoint;
        wound.WoundSeverityPoint = FixedPoint2.Clamp(ApplySeverityModifiers(uid, severity), 0, WoundMaxSeverity);

        if (wound.WoundSeverityPoint != old)
        {
            var ev = new WoundSeverityPointChangedEvent(uid, wound, old, severity);
            RaiseLocalEvent(uid, ref ev);
        }

        CheckSeverityThresholds(uid, wound);
        Dirty(uid, wound);

        UpdateWoundableIntegrity(wound.Parent);
        CheckWoundableSeverityThresholds(wound.Parent);
    }

    /// <summary>
    /// Applies severity to a wound
    /// </summary>
    /// <param name="uid">UID of the wound.</param>
    /// <param name="severity">Severity to add.</param>
    /// <param name="wound">Wound to which severity is applied.</param>
    /// <param name="isHealing">Name speaks for this.</param>
    /// <param name="traumaList">Traumas to apply when applying severity.. Please use _trauma.RandomTraumaChance if you expect your thing to apply traumas.</param>
    public void ApplyWoundSeverity(
        EntityUid uid,
        FixedPoint2 severity,
        WoundComponent? wound = null,
        bool isHealing = false,
        IEnumerable<TraumaType>? traumaList = null)
    {
        if (!Resolve(uid, ref wound) || _net.IsClient)
            return;

        var old = wound.WoundSeverityPoint;

        wound.WoundSeverityPoint = FixedPoint2.Clamp(isHealing ? old - severity : old + ApplySeverityModifiers(wound.Parent, severity), 0, WoundMaxSeverity);
        // Apply modifiers, actually we've got "raw" severity up here.
        // Healing modifiers are applied, so doing this again isn't needed.
        // why would you even want a wound with more than 100 severity

        if (wound.WoundSeverityPoint != old)
        {
            var ev = new WoundSeverityPointChangedEvent(uid, wound, old, severity);
            RaiseLocalEvent(uid, ref ev);
        }

        // if the said wound didn't open with a trauma-inducing effect, we can't make it inflict a trauma.. so yeah
        if (wound.CanApplyTrauma)
        {
            var traumasToApply = traumaList ?? _trauma.RandomTraumaChance(wound.Parent, severity);
            wound.WoundType =
                _trauma.TryApplyTrauma(wound.Parent, severity, traumasToApply)
                    ? WoundType.Internal
                    : WoundType.External;
        }
        // wounds that cause traumas are separated from those that don't.

        CheckSeverityThresholds(uid, wound);
        Dirty(uid, wound);

        UpdateWoundableIntegrity(wound.Parent);
        CheckWoundableSeverityThresholds(wound.Parent);
    }

    public FixedPoint2 ApplySeverityModifiers(
        EntityUid woundable,
        FixedPoint2 severity,
        WoundableComponent? component = null)
    {
        if (!Resolve(woundable, ref component))
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
    public bool TryAddWoundableSeverityMultiplier(
        EntityUid uid,
        EntityUid owner,
        FixedPoint2 change,
        string identifier,
        WoundableComponent? component = null)
    {
        if (!Resolve(uid, ref component) || !_net.IsServer)
            return false;

        if (!component.SeverityMultipliers.TryAdd(owner, new WoundableSeverityMultiplier(change, identifier)))
            return false;

        foreach (var wound in component.Wounds!.ContainedEntities)
        {
            CheckSeverityThresholds(wound);
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
    public bool TryRemoveWoundableSeverityMultiplier(
        EntityUid uid,
        string identifier,
        WoundableComponent? component = null)
    {
        if (!Resolve(uid, ref component) || !_net.IsServer)
            return false;

        foreach (var multiplier in component.SeverityMultipliers.Where(multiplier => multiplier.Value.Identifier == identifier))
        {
            if (!component.SeverityMultipliers.Remove(multiplier.Key, out _))
                return false;

            foreach (var wound in component.Wounds!.ContainedEntities)
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
    public bool TryChangeWoundableSeverityMultiplier(
        EntityUid uid,
        string identifier,
        FixedPoint2 change,
        WoundableComponent? component = null)
    {
        if (!Resolve(uid, ref component) || !_net.IsServer)
            return false;

        foreach (var multiplier in component.SeverityMultipliers.Where(multiplier => multiplier.Value.Identifier == identifier).ToList())
        {
            component.SeverityMultipliers.Remove(multiplier.Key, out var value);

            value.Change = change;
            component.SeverityMultipliers.Add(multiplier.Key, value);

            foreach (var wound in component.Wounds!.ContainedEntities.ToList())
            {
                CheckSeverityThresholds(wound);
            }

            UpdateWoundableIntegrity(uid, component);

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
            return;

        var key = bodyPart.ToHumanoidLayers();
        if (key == null)
            return;

        // clean container triggers an enumeration fatal error
        woundableComp.AllowWounds = false;
        woundableComp.ForceLoss = true;
        Dirty(woundableEntity, woundableComp);

        if (IsWoundableRoot(woundableEntity, woundableComp))
        {
            DestroyWoundableChildren(woundableEntity, woundableComp);
            var excludedStuff = new List<string>
            {
                WoundContainerId,
                BoneContainerId,
            };

            _body.GibBody(bodyPart.Body.Value, excludedContainers: excludedStuff);

            QueueDel(woundableEntity); // More blood for the blood God!
        }
        else
        {
            if (!_container.TryGetContainingContainer(parentWoundableEntity, woundableEntity, out var container))
                return;

            _body.DropSlotContents(new Entity<BodyPartComponent>(woundableEntity, Comp<BodyPartComponent>(woundableEntity)));
            var bodyPartId = container.ID;

            DestroyWoundableChildren(woundableEntity, woundableComp);

            _body.DetachPart(parentWoundableEntity, bodyPartId.Remove(0, 15), woundableEntity);
            QueueDel(woundableEntity);
        }
    }

    /// <summary>
    /// Amputates (not destroys) an entity's body part if conditions are met.
    /// </summary>
    /// <param name="parentWoundableEntity">Parent of the woundable entity. Yes.</param>
    /// <param name="woundableEntity">The entity containing the vulnerable body part</param>
    /// <param name="woundableComp">Woundable component of woundableEntity.</param>
    public void AmputateWoundable(EntityUid parentWoundableEntity, EntityUid woundableEntity, WoundableComponent woundableComp)
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
        Dirty(woundableEntity, woundableComp);

        DestroyWoundableChildren(woundableEntity, woundableComp);
        _body.DetachPart(parentWoundableEntity, bodyPartId.Remove(0, 15), woundableEntity);
    }

    #endregion

    #region Private API

    private void InsertBoneIntoWoundable(EntityUid uid, WoundableComponent? comp = null)
    {
        if (!Resolve(uid, ref comp, false) || _net.IsClient)
            return;

        var bone = Spawn(BoneEntityId);

        if (!TryComp<BoneComponent>(bone, out var boneComp))
            return;

        boneComp.BoneWoundable = uid;

        _transform.SetParent(bone, uid);
        _container.Insert(bone, comp.Bone!);
    }

    private void TransferWoundDamage(EntityUid parent, EntityUid wound, WoundComponent woundComp, WoundableComponent? woundableComp = null)
    {
        if (!Resolve(parent, ref woundableComp))
            return;

        if (!TryContinueWound(parent, MetaData(wound).EntityPrototype!.ID, woundComp.WoundSeverityPoint / 4, woundableComp))
        {
            TryCreateWound(
                parent,
                MetaData(wound).EntityPrototype!.ID,
                woundComp.WoundSeverityPoint / 4,
                woundComp.DamageGroup!,
                woundableComp);
        }
    }

    private void UpdateWoundableIntegrity(EntityUid uid, WoundableComponent? component = null)
    {
        if (!Resolve(uid, ref component, false))
            return;

        var damage =
            component.Wounds!.ContainedEntities.Select(Comp<WoundComponent>)
                .Aggregate((FixedPoint2) 0, (current, comp) => current + comp.WoundSeverityPoint * comp.WoundableIntegrityMultiplier);

        var newIntegrity = FixedPoint2.Clamp(component.IntegrityCap - damage, 0, component.IntegrityCap);
        if (newIntegrity == component.WoundableIntegrity)
            return;

        component.WoundableIntegrity = newIntegrity;
        Dirty(uid, component);

        var ev = new WoundableIntegrityChangedEvent(uid, component.WoundableIntegrity);
        RaiseLocalEvent(uid, ref ev);
    }

    protected bool AddWound(
        EntityUid target,
        EntityUid wound,
        FixedPoint2 woundSeverity,
        string damageGroup,
        WoundableComponent? woundableComponent = null,
        WoundComponent? woundComponent = null,
        IEnumerable<TraumaType>? traumaList = null)
    {
        if (!Resolve(target, ref woundableComponent, false)
            || !Resolve(wound, ref woundComponent, false)
            || woundableComponent.Wounds!.Contains(wound))
            return false;

        _transform.SetParent(wound, target);

        woundComponent.Parent = target;
        woundComponent.DamageGroup = damageGroup;

        SetWoundSeverity(wound, woundSeverity);

        if (woundComponent.CanApplyTrauma)
        {
            var traumasToApply = traumaList ?? _trauma.RandomTraumaChance(target, woundSeverity, woundableComponent);
            woundComponent.WoundType =
                _trauma.TryApplyTrauma(target, woundSeverity, traumasToApply)
                    ? WoundType.Internal
                    : WoundType.External;
        }

        Dirty(wound, woundComponent);
        Dirty(target, woundableComponent);

        if (!_container.Insert(wound, woundableComponent.Wounds))
            return false;

        var woundMeta = MetaData(wound);
        var targetMeta = MetaData(target);

        _sawmill.Info($"Wound: {woundMeta.EntityPrototype!.ID}({wound}) created on {targetMeta.EntityPrototype!.ID}({target})");

        Dirty(wound, woundComponent);
        Dirty(target, woundableComponent);

        return true;
    }

    protected bool RemoveWound(EntityUid woundEntity, WoundComponent? wound = null)
    {
        if (!Resolve(woundEntity, ref wound, false)
            || !TryComp(wound.Parent, out WoundableComponent? woundable)
            || woundable.Wounds == null)
            return false;

        _sawmill.Info($"Wound: {MetaData(woundEntity).EntityPrototype!.ID}({woundEntity}) created on {MetaData(wound.Parent).EntityPrototype!.ID}({wound.Parent})");

        Dirty(wound.Parent, woundable);

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

    private void CheckSeverityThresholds(EntityUid wound, WoundComponent? component = null)
    {
        if (!Resolve(wound, ref component) || !_net.IsServer)
            return;

        var nearestSeverity = component.WoundSeverity;
        foreach (var (severity, value) in _woundThresholds.OrderByDescending(kv => kv.Value))
        {
            if (component.WoundSeverityPoint < value)
                continue;

            if (severity == WoundSeverity.Healed && component.WoundSeverityPoint > 0)
                continue;

            nearestSeverity = severity;
            break;
        }

        if (nearestSeverity != component.WoundSeverity)
        {
            var ev = new WoundSeverityChangedEvent(wound, nearestSeverity);
            RaiseLocalEvent(wound, ref ev, true);
        }
        component.WoundSeverity = nearestSeverity;

        Dirty(wound, component);

        var bodyPart = Comp<BodyPartComponent>(component.Parent);
        if (bodyPart.Body == null)
            return;

        _appearance.SetData(bodyPart.Body.Value, WoundableVisualizerKeys.Update, component.WoundSeverity);
    }

    private void CheckWoundableSeverityThresholds(EntityUid woundable, WoundableComponent? component = null)
    {
        if (!Resolve(woundable, ref component, false) || !_net.IsServer)
            return;

        var nearestSeverity = component.WoundableSeverity;
        foreach (var (severity, value) in component.Thresholds.OrderByDescending(kv => kv.Value))
        {
            if (component.WoundableIntegrity >= component.IntegrityCap)
            {
                nearestSeverity = WoundableSeverity.Healthy;
                break;
            }

            if (component.WoundableIntegrity < value)
                continue;

            nearestSeverity = severity;
            break;
        }

        if (component.ForceLoss)
            nearestSeverity = WoundableSeverity.Loss;

        if (nearestSeverity != component.WoundableSeverity)
        {
            var ev = new WoundableSeverityChangedEvent(woundable, nearestSeverity);
            RaiseLocalEvent(woundable, ref ev, true);
        }
        component.WoundableSeverity = nearestSeverity;

        if (!TryChangeWoundableSeverityMultiplier(woundable, "WoundableSeverity", _thresholdMultipliers[component.WoundableSeverity], component))
        {
            TryAddWoundableSeverityMultiplier(
                woundable,
                woundable,
                _thresholdMultipliers[component.WoundableSeverity],
                "WoundableSeverity",
                component);
        }

        Dirty(woundable, component);

        var bodyPart = Comp<BodyPartComponent>(woundable);
        if (bodyPart.Body == null)
            return;

        _appearance.SetData(bodyPart.Body.Value, WoundableVisualizerKeys.Update, component.WoundableIntegrity); // don't mind
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

    private void DestroyWoundableChildren(EntityUid woundableEntity, WoundableComponent? woundableComp = null)
    {
        if (!Resolve(woundableEntity, ref woundableComp))
            return;

        foreach (var child in woundableComp.ChildWoundables)
        {
            if (woundableComp.WoundableSeverity is WoundableSeverity.Critical)
            {
                DestroyWoundable(woundableEntity, child, Comp<WoundableComponent>(child));
                continue;
            }

            AmputateWoundable(woundableEntity, child, Comp<WoundableComponent>(child));
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

            if (!TryComp<WoundableComponent>(id, out var woundable))
                continue;

            result[target.Value] = woundable.WoundableSeverity;
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
