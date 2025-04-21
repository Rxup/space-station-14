using System.Linq;
using Content.Shared.Backmen.Surgery.Traumas;
using Content.Shared.Backmen.Surgery.Traumas.Systems;
using Content.Shared.Backmen.Surgery.Wounds.Components;
using Content.Shared.Backmen.Targeting;
using Content.Shared.Body.Components;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
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

namespace Content.Shared.Backmen.Surgery.Wounds.Systems;

public abstract partial class WoundSystem : EntitySystem
{
    [Dependency] protected readonly IRobustRandom Random = default!;
    [Dependency] protected readonly IConfigurationManager Cfg = default!;

    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly IComponentFactory _factory = default!;

    [Dependency] protected readonly SharedBodySystem Body = default!;
    [Dependency] protected readonly SharedHandsSystem Hands = default!;

    [Dependency] protected readonly SharedContainerSystem Containers = default!;
    [Dependency] protected readonly SharedTransformSystem Xform = default!;

    [Dependency] protected readonly SharedAudioSystem Audio = default!;

    [Dependency] protected readonly ThrowingSystem Throwing = default!;
    [Dependency] protected readonly InventorySystem Inventory = default!;
    [Dependency] protected readonly TraumaSystem Trauma = default!;

    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;

    protected readonly Dictionary<WoundSeverity, FixedPoint2> WoundThresholds = new()
    {
        { WoundSeverity.Healed, 0 },
        { WoundSeverity.Minor, 1 },
        { WoundSeverity.Moderate, 25 },
        { WoundSeverity.Severe, 50 },
        { WoundSeverity.Critical, 80 },
        { WoundSeverity.Loss, 100 },
    };

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<WoundComponent, ComponentGetState>(OnWoundComponentGet);
        SubscribeLocalEvent<WoundComponent, ComponentHandleState>(OnWoundComponentHandleState);

        SubscribeLocalEvent<WoundableComponent, ComponentGetState>(OnWoundableComponentGet);
        SubscribeLocalEvent<WoundableComponent, ComponentHandleState>(OnWoundableComponentHandleState);

        InitWounding();
    }

    private void OnWoundComponentGet(EntityUid uid, WoundComponent comp, ref ComponentGetState args)
    {
        var state = new WoundComponentState
        {
            HoldingWoundable =
                TryGetNetEntity(comp.HoldingWoundable, out var holdingWoundable)
                    ? holdingWoundable.Value
                    : NetEntity.Invalid,

            WoundSeverityPoint = comp.WoundSeverityPoint,
            WoundableIntegrityMultiplier = comp.WoundableIntegrityMultiplier,

            WoundType = comp.WoundType,

            DamageGroup = comp.DamageGroup,
            DamageType = comp.DamageType,

            ScarWound = comp.ScarWound,
            IsScar = comp.IsScar,

            WoundSeverity = comp.WoundSeverity,

            WoundVisibility = comp.WoundVisibility,

            CanBeHealed = comp.CanBeHealed,
        };

        args.State = state;
    }

    private void OnWoundComponentHandleState(EntityUid uid, WoundComponent component, ref ComponentHandleState args)
    {
        if (args.Current is not WoundComponentState state)
            return;

        // Predict events on client!!
        var holdingWoundable = TryGetEntity(state.HoldingWoundable, out var e) ? e.Value : EntityUid.Invalid;
        if (holdingWoundable != component.HoldingWoundable)
        {
            if (holdingWoundable == EntityUid.Invalid)
            {
                if (TryComp(holdingWoundable, out WoundableComponent? oldParentWoundable) &&
                    TryComp(oldParentWoundable.RootWoundable, out WoundableComponent? oldWoundableRoot))
                {
                    var ev2 = new WoundRemovedEvent(component, oldParentWoundable, oldWoundableRoot);
                    RaiseLocalEvent(component.HoldingWoundable, ref ev2);
                }
            }
            else
            {
                if (TryComp(holdingWoundable, out WoundableComponent? parentWoundable) &&
                    TryComp(parentWoundable.RootWoundable, out WoundableComponent? woundableRoot))
                {
                    var ev = new WoundAddedEvent(component, parentWoundable, woundableRoot);
                    RaiseLocalEvent(uid, ref ev);

                    var ev1 = new WoundAddedEvent(component, parentWoundable, woundableRoot);
                    RaiseLocalEvent(holdingWoundable, ref ev1);

                    var bodyPart = Comp<BodyPartComponent>(holdingWoundable);
                    if (bodyPart.Body.HasValue)
                    {
                        var ev2 = new WoundAddedOnBodyEvent((uid, component), parentWoundable, woundableRoot);
                        RaiseLocalEvent(bodyPart.Body.Value, ref ev2);
                    }
                }
            }
        }

        component.HoldingWoundable = holdingWoundable;

        if (component.WoundSeverityPoint != state.WoundSeverityPoint)
        {
            var ev = new WoundSeverityPointChangedEvent(component,
                component.WoundSeverityPoint,
                state.WoundSeverityPoint);
            RaiseLocalEvent(uid, ref ev);

            // TODO: On body changed events aren't predicted, welp
        }

        component.WoundSeverityPoint = state.WoundSeverityPoint;
        component.WoundableIntegrityMultiplier = state.WoundableIntegrityMultiplier;

        if (component.HoldingWoundable != EntityUid.Invalid)
        {
            UpdateWoundableIntegrity(component.HoldingWoundable);
            CheckWoundableSeverityThresholds(component.HoldingWoundable);
        }

        component.WoundType = state.WoundType;

        component.DamageGroup = state.DamageGroup;
        if (state.DamageType != null)
            component.DamageType = state.DamageType;

        component.ScarWound = state.ScarWound;
        component.IsScar = state.IsScar;

        if (component.WoundSeverity != state.WoundSeverity)
        {
            var ev = new WoundSeverityChangedEvent(component.WoundSeverity, state.WoundSeverity);
            RaiseLocalEvent(uid, ref ev);
        }

        component.WoundSeverity = state.WoundSeverity;

        component.WoundVisibility = state.WoundVisibility;
        component.CanBeHealed = state.CanBeHealed;
    }

    private void OnWoundableComponentGet(EntityUid uid, WoundableComponent comp, ref ComponentGetState args)
    {
        var state = new WoundableComponentState
        {
            ParentWoundable = TryGetNetEntity(comp.ParentWoundable, out var parentWoundable) ? parentWoundable : null,
            RootWoundable = TryGetNetEntity(comp.RootWoundable, out var rootWoundable)
                ? rootWoundable.Value
                : NetEntity.Invalid,

            ChildWoundables =
                comp.ChildWoundables
                    .Select(woundable => TryGetNetEntity(woundable, out var ne)
                        ? ne.Value
                        : NetEntity.Invalid)
                    .ToHashSet(),
            // Attached and Detached -Woundable events are handled on client with containers

            AllowWounds = comp.AllowWounds,

            DamageContainerID = comp.DamageContainerID,

            DodgeChance = comp.DodgeChance,

            WoundableIntegrity = comp.WoundableIntegrity,
            HealAbility = comp.HealAbility,

            SeverityMultipliers =
                comp.SeverityMultipliers
                    .Select(multiplier
                        => (TryGetNetEntity(multiplier.Key, out var ne) ? ne.Value : NetEntity.Invalid,
                            multiplier.Value))
                    .ToDictionary(),
            HealingMultipliers =
                comp.HealingMultipliers
                    .Select(multiplier
                        => (TryGetNetEntity(multiplier.Key, out var ne) ? ne.Value : NetEntity.Invalid,
                            multiplier.Value))
                    .ToDictionary(),

            WoundableSeverity = comp.WoundableSeverity,
            HealingRateAccumulated = comp.HealingRateAccumulated,
        };

        args.State = state;
    }

    private void OnWoundableComponentHandleState(EntityUid uid,
        WoundableComponent component,
        ref ComponentHandleState args)
    {
        if (args.Current is not WoundableComponentState state)
            return;

        TryGetEntity(state.ParentWoundable, out component.ParentWoundable);
        TryGetEntity(state.RootWoundable, out var rootWoundable);
        component.RootWoundable = rootWoundable ?? EntityUid.Invalid;

        component.ChildWoundables = state.ChildWoundables
            .Select(x => TryGetEntity(x, out var y) ? y.Value : EntityUid.Invalid)
            .Where(x => x.Valid)
            .ToHashSet();
        // Attached and Detached -Woundable events are handled on client with containers

        component.AllowWounds = state.AllowWounds;

        component.DamageContainerID = state.DamageContainerID;

        component.DodgeChance = state.DodgeChance;
        component.HealAbility = state.HealAbility;

        component.SeverityMultipliers =
            state.SeverityMultipliers
                .Select(multiplier
                    => (TryGetEntity(multiplier.Key, out var ne) ? ne.Value : EntityUid.Invalid, multiplier.Value))
                .ToDictionary();
        component.HealingMultipliers =
            state.HealingMultipliers
                .Select(multiplier
                    => (TryGetEntity(multiplier.Key, out var ne) ? ne.Value : EntityUid.Invalid, multiplier.Value))
                .ToDictionary();

        if (component.WoundableIntegrity != state.WoundableIntegrity)
        {
            var bodyPart = Comp<BodyPartComponent>(uid);

            var ev = new WoundableIntegrityChangedEvent(component.WoundableIntegrity, state.WoundableIntegrity);
            RaiseLocalEvent(uid, ref ev);

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

                        // The first check is for the root (chest) part entities, the other one is for attached entities
                        if (woundable.Comp.RootWoundable == woundable.Owner && woundable.Owner != rootPart)
                            continue;

                        bodySeverity += GetWoundableIntegrityDamage(woundable, woundable);
                    }
                }

                var ev1 = new WoundableIntegrityChangedOnBodyEvent(
                    (uid, component),
                    bodySeverity - (component.WoundableIntegrity - state.WoundableIntegrity),
                    bodySeverity);
                RaiseLocalEvent(bodyPart.Body.Value, ref ev1);
            }
        }

        component.WoundableIntegrity = state.WoundableIntegrity;

        if (component.WoundableSeverity != state.WoundableSeverity)
        {
            var ev = new WoundableSeverityChangedEvent(component.WoundableSeverity, state.WoundableSeverity);
            RaiseLocalEvent(uid, ref ev);
        }

        component.WoundableSeverity = state.WoundableSeverity;

        component.HealingRateAccumulated = state.HealingRateAccumulated;
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

            if (severity == WoundSeverity.Healed)
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
            Dirty(wound, component);
    }

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

        var ev = new WoundSeverityPointChangedEvent(wound, oldSeverity, wound.WoundSeverityPoint);
        RaiseLocalEvent(uid, ref ev);

        var bodyPart = Comp<BodyPartComponent>(wound.HoldingWoundable);
        if (!bodyPart.Body.HasValue)
            return;

        var bodySeverity = FixedPoint2.Zero;

        var rootPart = Comp<BodyComponent>(bodyPart.Body.Value).RootContainer.ContainedEntity;
        if (rootPart.HasValue)
        {
            bodySeverity =
                GetAllWoundableChildren(rootPart.Value)
                    .Aggregate(bodySeverity,
                        (current, woundable) => current + GetWoundableSeverityPoint(woundable, woundable));
        }

        var ev1 = new WoundSeverityPointChangedOnBodyEvent(
            (uid, wound),
            bodySeverity - (wound.WoundSeverityPoint - oldSeverity),
            bodySeverity);
        RaiseLocalEvent(bodyPart.Body.Value, ref ev1);
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
        Dirty(uid, component);
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

        Dirty(woundable, component);

        var bodyPart = Comp<BodyPartComponent>(woundable);
        if (bodyPart.Body == null)
            return;

        if (!TryComp<TargetingComponent>(bodyPart.Body.Value, out var targeting))
            return;

        targeting.BodyStatus = GetWoundableStatesOnBodyPainFeels(bodyPart.Body.Value);
        Dirty(bodyPart.Body.Value, targeting);

        if (_net.IsServer)
            RaiseNetworkEvent(new TargetIntegrityChangeEvent(GetNetEntity(bodyPart.Body.Value)), bodyPart.Body.Value);
    }

    protected void FixWoundableRoots(EntityUid targetEntity, WoundableComponent targetWoundable)
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

        var woundableRoot = WoundableQuery.Comp(parentWoundable.RootWoundable);
        var woundableAttached = new WoundableAttachedEvent(parentEntity, parentWoundable);

        RaiseLocalEvent(childEntity, ref woundableAttached);

        var bodyPart = Comp<BodyPartComponent>(childEntity);
        foreach (var (woundId, wound) in GetAllWounds(childEntity, childWoundable))
        {
            var ev = new WoundAddedEvent(wound, parentWoundable, woundableRoot);
            RaiseLocalEvent(woundId, ref ev);

            if (bodyPart.Body.HasValue)
            {
                var ev2 = new WoundAddedOnBodyEvent((woundId, wound), parentWoundable, woundableRoot);
                RaiseLocalEvent(bodyPart.Body.Value, ref ev2);
            }
        }

        Dirty(childEntity, childWoundable);
        Dirty(parentEntity, parentWoundable);
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

        Dirty(childEntity, childWoundable);
        Dirty(parentEntity, parentWoundable);
    }

    protected void DropWoundableOrgans(EntityUid woundable, WoundableComponent? woundableComp)
    {
        if (!WoundableQuery.Resolve(woundable, ref woundableComp, false))
            return;

        foreach (var organ in Body.GetPartOrgans(woundable))
        {
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
            if (childWoundable.WoundableSeverity is WoundableSeverity.Critical)
            {
                DestroyWoundable(woundableEntity, child, childWoundable);
                continue;
            }

            AmputateWoundable(woundableEntity, child, childWoundable);
        }
    }
}
