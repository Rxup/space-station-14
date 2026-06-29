using System.Collections.Generic;
using System.Linq;
using Content.Shared.Backmen.Body.OrganRelations;
using Content.Shared.Backmen.Surgery.Consciousness.Components;
using Content.Shared.Backmen.Surgery.Wounds.Components;
using Content.Shared.Backmen.Targeting;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Shared.Backmen.Damage;

/// <summary>
/// Backmen damage-model dispatch, container resolution, and Injurable handler wiring.
/// </summary>
public sealed partial class BackmenDamageModelSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _prototypeManager = default!;
    [Dependency] private IComponentFactory _componentFactory = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private BackmenDamageModelExclusivitySystem _exclusivity = default!;

    [Dependency] private EntityQuery<DamageableComponent> _damageableQuery = default!;
    [Dependency] private EntityQuery<InjurableComponent> _injurableQuery = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<InjurableComponent, DamageDealtEvent>(OnInjurableDamageDealt);
    }

    /// <summary>
    /// Applies damage-type aliases and dispatches through <see cref="DamageDealtEvent"/> when a damage model is present.
    /// </summary>
    /// <returns>True when dispatch handled the write path (caller should return <paramref name="dispatchedDamage"/>).</returns>
    public bool TryDispatchModelDamage(
        Entity<DamageableComponent> ent,
        DamageSpecifier damage,
        EntityUid? origin,
        bool interruptsDoAfters,
        TargetBodyPart? targetPart,
        out DamageSpecifier dispatchedDamage)
    {
        dispatchedDamage = DamageSpecifierAliases.ApplyDamageTypeAliases(damage, _prototypeManager);

        if (!HasDamageModel(ent))
            return false;

        var evt = new DamageDealtEvent(dispatchedDamage, origin, interruptsDoAfters, targetPart);
        RaiseLocalEvent(ent, ref evt);
        return true;
    }

    public void ApplyDamageToDamageable(
        Entity<DamageableComponent> ent,
        DamageSpecifier damage,
        ProtoId<DamageContainerPrototype>? container,
        EntityUid? origin,
        bool interruptsDoAfters) =>
        _damageable.ApplyDamageToDamageable(ent, damage, container, origin, interruptsDoAfters);


    public bool CanBeDamagedBy(Entity<DamageableComponent?> ent, ProtoId<DamageTypePrototype> type)
    {
        if (!_damageableQuery.Resolve(ent, ref ent.Comp, false))
            return false;

        if (!TryGetDamageContainer(ent, out var container))
            return false;

        type = ResolveEffectiveDamageType(type);
        if (!_damageable.SupportsDamageType(container, type))
            return false;

        if (!_exclusivity.HasExclusiveBackmenDamageModel(ent.Owner))
            return true;

        return HasWoundDamagePrototype(type);
    }

    private bool HasWoundDamagePrototype(ProtoId<DamageTypePrototype> type) =>
        _prototypeManager.TryIndex<EntityPrototype>(type, out var proto)
        && proto.TryGetComponent<WoundComponent>(out _, _componentFactory);

    private ProtoId<DamageTypePrototype> ResolveEffectiveDamageType(ProtoId<DamageTypePrototype> type)
    {
        if (_prototypeManager.TryIndex(type, out var proto) && proto.AppliesAs is { } appliesAs)
            return appliesAs;

        return type;
    }

    public bool TryGetDamageContainer(EntityUid uid, out ProtoId<DamageContainerPrototype>? container)
    {
        if (TryComp<InjurableComponent>(uid, out var injurable))
        {
            container = injurable.DamageContainer;
            return true;
        }

        if (TryComp<WoundableComponent>(uid, out var woundable))
        {
            container = woundable.DamageContainer;
            return true;
        }

        if (TryComp<ConsciousnessComponent>(uid, out var consciousness))
        {
            container = consciousness.DamageContainer;
            return true;
        }

        if (TryComp<BkmDetachedBodyComponent>(uid, out var detached)
            && detached.RootOrgan is { } root
            && TryComp<WoundableComponent>(root, out var rootWoundable))
        {
            container = rootWoundable.DamageContainer;
            return true;
        }

        container = null;
        return false;
    }

    public bool MatchesDamageContainerFilter(EntityUid uid, IEnumerable<string> allowedContainers)
    {
        if (!TryGetDamageContainer(uid, out var container) || container is not { } containerId)
            return false;

        return allowedContainers.Contains(containerId.Id);
    }

    private bool HasDamageModel(EntityUid uid) =>
        _exclusivity.HasExclusiveBackmenDamageModel(uid)
        || _injurableQuery.HasComponent(uid);

    private void OnInjurableDamageDealt(Entity<InjurableComponent> ent, ref DamageDealtEvent args)
    {
        if (_exclusivity.HasExclusiveBackmenDamageModel(ent))
            return;

        if (!TryComp<DamageableComponent>(ent, out var damageable))
            return;

        ApplyDamageToDamageable(
            (ent, damageable),
            args.Damage,
            ent.Comp.DamageContainer,
            args.Origin,
            args.InterruptsDoAfters);
    }
}
