using System.Linq;
using Content.Shared.Backmen.Surgery.Traumas;
using Content.Shared.Backmen.Surgery.Traumas.Systems;
using Content.Shared.Backmen.Surgery.Wounds.Components;
using Content.Shared.Backmen.Targeting;
using Content.Shared.Body.Components;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.FixedPoint;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Throwing;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Containers;
using Robust.Shared.GameStates;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Shared.Backmen.Surgery.Wounds.Systems;

public abstract partial class WoundSystem : EntitySystem
{
    [Dependency] protected IRobustRandom Random = default!;
    [Dependency] protected IConfigurationManager Cfg = default!;

    [Dependency] protected IGameTiming Timing = default!;

    [Dependency] private INetManager _net = default!;
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private IComponentFactory _factory = default!;

    [Dependency] protected SharedBodySystem Body = default!;
    [Dependency] protected SharedHandsSystem Hands = default!;

    [Dependency] protected SharedContainerSystem Containers = default!;
    [Dependency] protected SharedTransformSystem Xform = default!;

    [Dependency] protected SharedAudioSystem Audio = default!;

    [Dependency] protected ThrowingSystem Throwing = default!;
    [Dependency] protected InventorySystem Inventory = default!;
    [Dependency] protected TraumaSystem Trauma = default!;
    [Dependency] protected MobStateSystem MobState = default!;

    [Dependency] private SharedPopupSystem _popup = default!;

    protected readonly Dictionary<WoundSeverity, FixedPoint2> WoundThresholds = new()
    {
        { WoundSeverity.Healed, 0 },
        { WoundSeverity.Minor, 1 },
        { WoundSeverity.Moderate, 25 },
        { WoundSeverity.Severe, 50 },
        { WoundSeverity.Critical, 80 },
        { WoundSeverity.Loss, 100 },
    };

    private readonly Dictionary<EntityUid, (EntityUid HoldingWoundable, FixedPoint2 SeverityPoint, WoundSeverity Severity)> _woundStateCache = new();
    private readonly Dictionary<EntityUid, (FixedPoint2 Integrity, WoundableSeverity Severity)> _woundableStateCache = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<WoundComponent, AfterAutoHandleStateEvent>(OnWoundAfterAutoHandleState);
        SubscribeLocalEvent<WoundComponent, ComponentShutdown>(OnWoundShutdown);
        SubscribeLocalEvent<WoundableComponent, AfterAutoHandleStateEvent>(OnWoundableAfterAutoHandleState);
        SubscribeLocalEvent<WoundableComponent, ComponentShutdown>(OnWoundableShutdown);
        SubscribeLocalEvent<WoundableComponent, EntityTerminatingEvent>(OnWoundableTerminating);

        InitWounding();
    }

    private void OnWoundShutdown(Entity<WoundComponent> ent, ref ComponentShutdown args)
    {
        _woundStateCache.Remove(ent);
    }

    private void OnWoundableShutdown(Entity<WoundableComponent> ent, ref ComponentShutdown args)
    {
        _woundableStateCache.Remove(ent);
    }

    private void OnWoundableTerminating(Entity<WoundableComponent> ent, ref EntityTerminatingEvent args)
    {
        SanitizeWoundableReferences(ent.Owner, ent.Comp);

        if (ent.Comp.ParentWoundable is not { } parentUid
            || !WoundableQuery.TryComp(parentUid, out var parentWoundable))
            return;

        if (!parentWoundable.ChildWoundables.Remove(ent.Owner) || TerminatingOrDeleted(parentUid))
            return;

        DirtyField(parentUid, parentWoundable, nameof(WoundableComponent.ChildWoundables));
    }

    private void OnWoundAfterAutoHandleState(Entity<WoundComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        SyncWoundDamageGroup(ent.Comp);

        if (!_net.IsClient)
        {
            _woundStateCache[ent] = (ent.Comp.HoldingWoundable, ent.Comp.WoundSeverityPoint, ent.Comp.WoundSeverity);
            return;
        }

        var old = _woundStateCache.GetValueOrDefault(ent);
        var holdingWoundable = ent.Comp.HoldingWoundable;

        if (holdingWoundable != old.HoldingWoundable)
        {
            if (holdingWoundable == EntityUid.Invalid)
            {
                if (old.HoldingWoundable != EntityUid.Invalid
                    && TryComp(old.HoldingWoundable, out WoundableComponent? oldParentWoundable)
                    && TryComp(oldParentWoundable.RootWoundable, out WoundableComponent? oldWoundableRoot))
                {
                    var ev2 = new WoundRemovedEvent(ent.Comp, oldParentWoundable, oldWoundableRoot);
                    RaiseLocalEvent(old.HoldingWoundable, ref ev2);
                }
            }
            else if (TryComp(holdingWoundable, out WoundableComponent? parentWoundable)
                     && TryComp(parentWoundable.RootWoundable, out WoundableComponent? woundableRoot))
            {
                var ev = new WoundAddedEvent(ent.Comp, parentWoundable, woundableRoot);
                RaiseLocalEvent(ent, ref ev);

                var ev1 = new WoundAddedEvent(ent.Comp, parentWoundable, woundableRoot);
                RaiseLocalEvent(holdingWoundable, ref ev1);
            }
        }

        if (ent.Comp.WoundSeverityPoint != old.SeverityPoint)
        {
            var ev = new WoundSeverityPointChangedEvent(ent.Comp, old.SeverityPoint, ent.Comp.WoundSeverityPoint);
            RaiseLocalEvent(ent, ref ev);
        }

        if (holdingWoundable != EntityUid.Invalid)
        {
            UpdateWoundableIntegrity(holdingWoundable);
            CheckWoundableSeverityThresholds(holdingWoundable);
        }

        if (ent.Comp.WoundSeverity != old.Severity)
        {
            var ev = new WoundSeverityChangedEvent(old.Severity, ent.Comp.WoundSeverity);
            RaiseLocalEvent(ent, ref ev);
        }

        _woundStateCache[ent] = (holdingWoundable, ent.Comp.WoundSeverityPoint, ent.Comp.WoundSeverity);
    }

    private void OnWoundableAfterAutoHandleState(Entity<WoundableComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        SanitizeWoundableReferences(ent.Owner, ent.Comp);

        if (!_net.IsClient)
        {
            _woundableStateCache[ent] = (ent.Comp.WoundableIntegrity, ent.Comp.WoundableSeverity);
            return;
        }

        var old = _woundableStateCache.GetValueOrDefault(ent);

        if (ent.Comp.WoundableIntegrity != old.Integrity)
        {
            var bodyPart = Comp<BodyPartComponent>(ent);

            var ev = new WoundableIntegrityChangedEvent(old.Integrity, ent.Comp.WoundableIntegrity);
            RaiseLocalEvent(ent, ref ev);

            var bodySeverity = FixedPoint2.Zero;
            if (bodyPart.Body.HasValue)
            {
                var rootPart = Comp<BodyComponent>(bodyPart.Body.Value).RootContainer.ContainedEntity;
                if (rootPart.HasValue)
                {
                    foreach (var woundable in GetAllWoundableChildren(rootPart.Value))
                    {
                        if (!MetaData(woundable).Initialized)
                            continue;

                        if (woundable.Comp.RootWoundable == woundable.Owner && woundable.Owner != rootPart)
                            continue;

                        bodySeverity += GetWoundableIntegrityDamage(woundable, woundable);
                    }
                }

                var ev1 = new WoundableIntegrityChangedOnBodyEvent(
                    ent,
                    bodySeverity - (old.Integrity - ent.Comp.WoundableIntegrity),
                    bodySeverity);
                RaiseLocalEvent(bodyPart.Body.Value, ref ev1);
            }
        }

        if (ent.Comp.WoundableSeverity != old.Severity)
        {
            var ev = new WoundableSeverityChangedEvent(old.Severity, ent.Comp.WoundableSeverity);
            RaiseLocalEvent(ent, ref ev);
        }

        _woundableStateCache[ent] = (ent.Comp.WoundableIntegrity, ent.Comp.WoundableSeverity);
    }

    private void SanitizeWoundableReferences(EntityUid owner, WoundableComponent component)
    {
        foreach (var key in component.SeverityMultipliers.Keys.ToArray())
        {
            if (TerminatingOrDeleted(key))
                component.SeverityMultipliers.Remove(key);
        }

        foreach (var key in component.HealingMultipliers.Keys.ToArray())
        {
            if (TerminatingOrDeleted(key))
                component.HealingMultipliers.Remove(key);
        }

        component.ChildWoundables.RemoveWhere(uid => TerminatingOrDeleted(uid));

        if (component.ParentWoundable is { } parent && TerminatingOrDeleted(parent))
            component.ParentWoundable = null;

        if (TerminatingOrDeleted(component.RootWoundable))
            component.RootWoundable = owner;
    }

    protected void SyncWoundDamageGroup(WoundComponent wound)
    {
        if (wound.NetworkedDamageGroup != null && _prototype.TryIndex(wound.NetworkedDamageGroup, out var damageGroup))
            wound.DamageGroup = damageGroup;
        else
            wound.DamageGroup = null;
    }

    protected void SetWoundDamageGroup(WoundComponent wound, DamageGroupPrototype? damageGroup)
    {
        wound.DamageGroup = damageGroup;
        wound.NetworkedDamageGroup = damageGroup?.ID;
    }

    protected void CheckSeverityThresholds(EntityUid wound, WoundComponent? component = null)
    {
        if (!WoundQuery.Resolve(wound, ref component, false))
            return;

        var nearestSeverity = component.WoundSeverity;
        foreach (var (severity, value) in WoundThresholds.OrderByDescending(kv => kv.Value))
        {
            if (component.WoundSeverityPoint < value)
                continue;

            nearestSeverity = severity;
            break;
        }

        if (nearestSeverity != component.WoundSeverity)
        {
            var ev = new WoundSeverityChangedEvent(component.WoundSeverity, nearestSeverity);
            RaiseLocalEvent(wound, ref ev);
        }
        component.WoundSeverity = nearestSeverity;

        if (!TerminatingOrDeleted(wound))
            DirtyField(wound, component, nameof(WoundComponent.WoundSeverity));
    }

    // NOTE: THIS SHOULD BE ONLY RAISED ON A CHANGE OF VALUES. OUTSIDE OF CLASSICAL DAMAGE HANDLING LIKE HEALING
    protected void RaiseWoundEvents(EntityUid uid,
        EntityUid woundableEnt,
        WoundComponent wound,
        FixedPoint2 oldSeverity,
        WoundableComponent? woundableComp = null)
    {
        if (!WoundableQuery.Resolve(woundableEnt, ref woundableComp, false) || woundableComp.Wounds == null)
            return;

        if (!woundableComp.Wounds.Contains(uid))
            return;

        var delta = wound.WoundSeverityPoint - oldSeverity;
        var damageSpec = new DamageSpecifier();

        damageSpec.DamageDict.Add(wound.DamageType, delta);

        var woundChangedEvent = new WoundChangedEvent(wound, delta);
        RaiseLocalEvent(uid, ref woundChangedEvent);

        // Raise woundable effects without computing the severity changes, so we do not accidentally duplicate the severity.
        GetWoundsChanged(woundableEnt, woundableEnt, damageSpec, false, woundableComp);

        var ev = new WoundSeverityPointChangedEvent(wound, oldSeverity, wound.WoundSeverityPoint);
        RaiseLocalEvent(uid, ref ev);
    }

    protected void UpdateWoundableIntegrity(EntityUid uid, WoundableComponent? component = null)
    {
        if (!WoundableQuery.Resolve(uid, ref component, false) || component.Wounds == null)
            return;

        // Ignore scars for woundable integrity.. Unless you want to confuse people with minor woundable state
        var damage =
            component.Wounds.ContainedEntities.Select(WoundQuery.Comp)
                .Where(wound => !wound.IsScar)
                .Aggregate(FixedPoint2.Zero, (current, wound) => current + wound.WoundIntegrityDamage);

        var newIntegrity = FixedPoint2.Clamp(component.IntegrityCap - damage, 0, component.IntegrityCap);
        if (newIntegrity == component.WoundableIntegrity)
            return;

        var ev = new WoundableIntegrityChangedEvent(component.WoundableIntegrity, newIntegrity);
        RaiseLocalEvent(uid, ref ev);

        var bodySeverity = FixedPoint2.Zero;
        var bodyPart = Comp<BodyPartComponent>(uid);

        if (bodyPart.Body.HasValue)
        {
            var rootPart = Comp<BodyComponent>(bodyPart.Body.Value).RootContainer.ContainedEntity;
            if (rootPart.HasValue)
            {
                bodySeverity =
                    GetAllWoundableChildren(rootPart.Value)
                        .Aggregate(bodySeverity,
                            (current, woundable) => current + GetWoundableIntegrityDamage(woundable, woundable));
            }

            var ev1 = new WoundableIntegrityChangedOnBodyEvent(
                (uid, component),
                bodySeverity - (component.WoundableIntegrity - newIntegrity),
                bodySeverity);
            RaiseLocalEvent(bodyPart.Body.Value, ref ev1);
        }

        component.WoundableIntegrity = newIntegrity;
        DirtyField(uid, component, nameof(WoundableComponent.WoundableIntegrity));
    }

    protected void CheckWoundableSeverityThresholds(EntityUid woundable, WoundableComponent? component = null)
    {
        if (!WoundableQuery.Resolve(woundable, ref component, false))
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

        if (nearestSeverity != component.WoundableSeverity)
        {
            var ev = new WoundableSeverityChangedEvent(component.WoundableSeverity, nearestSeverity);
            RaiseLocalEvent(woundable, ref ev);
        }

        component.WoundableSeverity = nearestSeverity;

        DirtyField(woundable, component, nameof(WoundableComponent.WoundableSeverity));

        var bodyPart = Comp<BodyPartComponent>(woundable);
        if (bodyPart.Body == null)
            return;

        if (!_net.IsServer)
            return;

        if (!TryComp<TargetingComponent>(bodyPart.Body.Value, out var targeting))
            return;

        targeting.BodyStatus = GetWoundableStatesOnBodyPainFeels(bodyPart.Body.Value);
        DirtyField(bodyPart.Body.Value, targeting, nameof(TargetingComponent.BodyStatus));
        RaiseNetworkEvent(new TargetIntegrityChangeEvent(GetNetEntity(bodyPart.Body.Value)), bodyPart.Body.Value);
    }

    protected void FixWoundableRoots(EntityUid targetEntity, WoundableComponent targetWoundable)
    {
        if (targetWoundable.ChildWoundables.Count == 0)
            return;

        foreach (var (childEntity, childWoundable) in GetAllWoundableChildren(targetEntity, targetWoundable))
        {
            childWoundable.RootWoundable = targetWoundable.RootWoundable;
            DirtyField(childEntity, childWoundable, nameof(WoundableComponent.RootWoundable));
        }

        DirtyField(targetEntity, targetWoundable, nameof(WoundableComponent.RootWoundable));
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

        var woundableRoot = CompOrNull<WoundableComponent>(parentWoundable.RootWoundable) ?? parentWoundable;
        var woundableAttached = new WoundableAttachedEvent(parentEntity, parentWoundable);

        RaiseLocalEvent(childEntity, ref woundableAttached);

        foreach (var (woundId, wound) in GetAllWounds(childEntity, childWoundable))
        {
            var ev = new WoundAddedEvent(wound, parentWoundable, woundableRoot);
            RaiseLocalEvent(woundId, ref ev);
        }

        DirtyFields(childEntity, childWoundable, null,
            nameof(WoundableComponent.ParentWoundable),
            nameof(WoundableComponent.RootWoundable),
            nameof(WoundableComponent.ChildWoundables));
        DirtyFields(parentEntity, parentWoundable, null,
            nameof(WoundableComponent.ChildWoundables),
            nameof(WoundableComponent.RootWoundable));
    }

    protected void InternalRemoveWoundableFromParent(
        EntityUid parentEntity,
        EntityUid childEntity,
        WoundableComponent parentWoundable,
        WoundableComponent childWoundable)
    {
        var removedFromParent = parentWoundable.ChildWoundables.Remove(childEntity);

        if (TerminatingOrDeleted(childEntity) || TerminatingOrDeleted(parentEntity))
        {
            if (removedFromParent && !TerminatingOrDeleted(parentEntity))
                DirtyField(parentEntity, parentWoundable, nameof(WoundableComponent.ChildWoundables));

            return;
        }
        childWoundable.ParentWoundable = null;
        childWoundable.RootWoundable = childEntity;

        FixWoundableRoots(childEntity, childWoundable);

        var oldWoundableRoot = WoundableQuery.Comp(parentWoundable.RootWoundable);
        var woundableDetached = new WoundableDetachedEvent(parentEntity, parentWoundable);

        RaiseLocalEvent(childEntity, ref woundableDetached);

        foreach (var (woundId, wound) in GetAllWounds(childEntity, childWoundable))
        {
            var ev = new WoundRemovedEvent(wound, childWoundable, oldWoundableRoot);
            RaiseLocalEvent(woundId, ref ev);

            var ev2 = new WoundRemovedEvent(wound, childWoundable, oldWoundableRoot);
            RaiseLocalEvent(childWoundable.RootWoundable, ref ev2);
        }

        DirtyFields(childEntity, childWoundable, null,
            nameof(WoundableComponent.ParentWoundable),
            nameof(WoundableComponent.RootWoundable),
            nameof(WoundableComponent.ChildWoundables));
        DirtyFields(parentEntity, parentWoundable, null,
            nameof(WoundableComponent.ChildWoundables),
            nameof(WoundableComponent.RootWoundable));
    }

    protected void DropWoundableOrgans(EntityUid woundable, WoundableComponent? woundableComp)
    {
        if (!WoundableQuery.Resolve(woundable, ref woundableComp, false))
            return;

        foreach (var organ in Body.GetPartOrgans(woundable))
        {
            if (TerminatingOrDeleted(organ.Id))
                continue;

            if (organ.Component.OrganSeverity == OrganSeverity.Normal)
            {
                // TODO: SFX for organs getting not destroyed, but thrown out
                Body.RemoveOrgan(organ.Id, organ.Component);
                Throwing.TryThrow(organ.Id, Random.NextAngle().ToWorldVec() * 7f, Random.Next(8, 24));
            }
            else
            {
                // Destroy it
                Trauma.TrySetOrganDamageModifier(
                    organ.Id,
                    organ.Component.OrganIntegrity * 100,
                    woundable,
                    WoundableDestroyalIdentifier,
                    organ.Component);
            }
        }
    }

    protected void DestroyWoundableChildren(EntityUid woundableEntity, WoundableComponent? woundableComp = null)
    {
        if (!WoundableQuery.Resolve(woundableEntity, ref woundableComp, false))
            return;

        foreach (var (child, childWoundable) in GetAllWoundableChildren(woundableEntity, woundableComp))
        {
            if (TerminatingOrDeleted(child))
                continue;

            if (childWoundable.WoundableSeverity is WoundableSeverity.Critical)
            {
                DestroyWoundable(woundableEntity, child, childWoundable);
                continue;
            }

            AmputateWoundable(woundableEntity, child, childWoundable);
        }
    }
}
