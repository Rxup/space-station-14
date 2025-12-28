using System.Linq;
using Content.Shared.Backmen.Surgery.Consciousness.Components;
using Content.Shared.Backmen.Surgery.Wounds;
using Content.Shared.Backmen.Surgery.Wounds.Components;
using Content.Shared.Backmen.Surgery.Wounds.Systems;
using Content.Shared.CCVar;
using Content.Shared.Chemistry;
using Content.Shared.Damage.Prototypes;
using Content.Shared.FixedPoint;
using Content.Shared.Inventory;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Body.Systems;
using Content.Shared.Radiation.Events;
using Content.Shared.Rejuvenate;
using Content.Shared.Backmen.Targeting;
using Content.Shared.Body.Components;
using Content.Shared.Damage.Components;
using Content.Shared.Explosion.EntitySystems;
using Robust.Shared.Configuration;
using Robust.Shared.GameStates;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Utility;

namespace Content.Shared.Damage.Systems;

public sealed partial class DamageableSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly INetManager _netMan = default!;
    [Dependency] private readonly MobThresholdSystem _mobThreshold = default!;
    [Dependency] private readonly SharedBodySystem _body = default!;
    [Dependency] private readonly IConfigurationManager _config = default!;
    [Dependency] private readonly SharedChemistryGuideDataSystem _chemistryGuideData = default!;
    [Dependency] private readonly SharedExplosionSystem _explosion = default!;

    // backmen edit start
    [Dependency] private readonly WoundSystem _wounds = default!;
    // backmen edit end

    private EntityQuery<AppearanceComponent> _appearanceQuery;
    private EntityQuery<DamageableComponent> _damageableQuery;

    public float UniversalAllDamageModifier { get; private set; } = 1f;
    public float UniversalAllHealModifier { get; private set; } = 1f;
    public float UniversalMeleeDamageModifier { get; private set; } = 1f;
    public float UniversalProjectileDamageModifier { get; private set; } = 1f;
    public float UniversalHitscanDamageModifier { get; private set; } = 1f;
    public float UniversalReagentDamageModifier { get; private set; } = 1f;
    public float UniversalReagentHealModifier { get; private set; } = 1f;
    public float UniversalExplosionDamageModifier { get; private set; } = 1f;
    public float UniversalThrownDamageModifier { get; private set; } = 1f;
    public float UniversalTopicalsHealModifier { get; private set; } = 1f;
    public float UniversalMobDamageModifier { get; private set; } = 1f;

    public override void Initialize()
    {
        SubscribeLocalEvent<DamageableComponent, ComponentInit>(DamageableInit);
        SubscribeLocalEvent<DamageableComponent, ComponentHandleState>(DamageableHandleState);
        SubscribeLocalEvent<DamageableComponent, ComponentGetState>(DamageableGetState);
        SubscribeLocalEvent<DamageableComponent, OnIrradiatedEvent>(OnIrradiated);
        SubscribeLocalEvent<DamageableComponent, RejuvenateEvent>(OnRejuvenate);

        _appearanceQuery = GetEntityQuery<AppearanceComponent>();
        _damageableQuery = GetEntityQuery<DamageableComponent>();

        // Damage modifier CVars
        Subs.CVar(_config, CCVars.PlaytestAllDamageModifier, value =>
        {
            UniversalAllDamageModifier = value;
            _chemistryGuideData.ReloadAllReagentPrototypes();
        }, true);

        Subs.CVar(_config, CCVars.PlaytestAllHealModifier, value =>
        {
            UniversalAllHealModifier = value;
            _chemistryGuideData.ReloadAllReagentPrototypes();
        }, true);

        Subs.CVar(_config, CCVars.PlaytestMeleeDamageModifier, value => UniversalMeleeDamageModifier = value, true);
        Subs.CVar(_config, CCVars.PlaytestProjectileDamageModifier, value => UniversalProjectileDamageModifier = value, true);
        Subs.CVar(_config, CCVars.PlaytestHitscanDamageModifier, value => UniversalHitscanDamageModifier = value, true);

        Subs.CVar(_config, CCVars.PlaytestReagentDamageModifier, value =>
        {
            UniversalReagentDamageModifier = value;
            _chemistryGuideData.ReloadAllReagentPrototypes();
        }, true);

        Subs.CVar(_config, CCVars.PlaytestReagentHealModifier, value =>
        {
            UniversalReagentHealModifier = value;
            _chemistryGuideData.ReloadAllReagentPrototypes();
        }, true);

        Subs.CVar(_config, CCVars.PlaytestExplosionDamageModifier, value => UniversalExplosionDamageModifier = value, true);
        Subs.CVar(_config, CCVars.PlaytestThrownDamageModifier, value => UniversalThrownDamageModifier = value, true);
        Subs.CVar(_config, CCVars.PlaytestTopicalsHealModifier, value => UniversalTopicalsHealModifier = value, true);
        Subs.CVar(_config, CCVars.PlaytestMobDamageModifier, value => UniversalMobDamageModifier = value, true);
    }

    private void DamageableInit(EntityUid uid, DamageableComponent component, ComponentInit _)
    {
        if (component.DamageContainerID != null &&
            _prototypeManager.TryIndex(component.DamageContainerID, out var damageContainerPrototype))
        {
            foreach (var type in damageContainerPrototype.SupportedTypes)
            {
                component.Damage.DamageDict.TryAdd(type, FixedPoint2.Zero);
            }

            foreach (var groupId in damageContainerPrototype.SupportedGroups)
            {
                var group = _prototypeManager.Index<DamageGroupPrototype>(groupId);
                foreach (var type in group.DamageTypes)
                {
                    component.Damage.DamageDict.TryAdd(type, FixedPoint2.Zero);
                }
            }
        }
        else
        {
            foreach (var type in _prototypeManager.EnumeratePrototypes<DamageTypePrototype>())
            {
                component.Damage.DamageDict.TryAdd(type.ID, FixedPoint2.Zero);
            }
        }

        component.Damage.GetDamagePerGroup(_prototypeManager, component.DamagePerGroup);
        component.TotalDamage = component.Damage.GetTotal();
    }

    public void SetDamage(EntityUid uid, DamageableComponent damageable, DamageSpecifier damage)
    {
        damageable.Damage = damage;
        OnEntityDamageChanged((uid, damageable));
    }

    /// <summary>
    ///     Called whenever damage on an entity actually changes.
    /// </summary>
    private void OnEntityDamageChanged(
        Entity<DamageableComponent> ent,
        DamageSpecifier? damageDelta = null,
        bool interruptsDoAfters = true,
        EntityUid? origin = null)
    {
        ent.Comp.Damage.GetDamagePerGroup(_prototypeManager, ent.Comp.DamagePerGroup);
        ent.Comp.TotalDamage = ent.Comp.Damage.GetTotal();
        Dirty(ent);

        if (damageDelta != null && _appearanceQuery.TryGetComponent(ent, out var appearance))
        {
            _appearance.SetData(
                ent,
                DamageVisualizerKeys.DamageUpdateGroups,
                new DamageVisualizerGroupData(ent.Comp.DamagePerGroup.Keys.ToList()),
                appearance);
        }

        RaiseLocalEvent(ent, new DamageChangedEvent(ent.Comp, damageDelta, interruptsDoAfters, origin));
    }

    [ByRefEvent]
    public record struct BeforeDamageChangedEvent(
        DamageSpecifier Damage,
        EntityUid? Origin = null,
        bool CanBeCancelled = true,
        bool Cancelled = false);

    // backmen edit start
    /// <summary>
    ///     Raised before damage is done and registered, so the system can check if you want to handle it manually.
    ///     Currently used for wounds.
    /// </summary>
    [ByRefEvent]
    public record struct HandleCustomDamage(
        DamageSpecifier Damage,
        TargetBodyPart? TargetPart,
        bool CanBeCancelled = true,
        EntityUid? Origin = null,
        bool Handled = false,
        DamageSpecifier? Damage = null);
    // backmen edit end

    public sealed class DamageModifyEvent : EntityEventArgs, IInventoryRelayEvent
    {
        public SlotFlags TargetSlots { get; } = ~SlotFlags.POCKET;
        public readonly DamageSpecifier OriginalDamage;
        public DamageSpecifier Damage;
        public EntityUid? Origin;
        public readonly TargetBodyPart? TargetPart;

        public DamageModifyEvent(DamageSpecifier damage, EntityUid? origin = null, TargetBodyPart? targetPart = null)
        {
            OriginalDamage = damage;
            Damage = damage;
            Origin = origin;
            TargetPart = targetPart;
        }
    }

    public DamageSpecifier? TryChangeDamage(EntityUid? uid,
        DamageSpecifier damage,
        bool ignoreResistances = false,
        bool interruptsDoAfters = true,
        DamageableComponent? damageable = null,
        EntityUid? origin = null,
        bool canBeCancelled = false,
        float partMultiplier = 1.00f,
        TargetBodyPart? targetPart = null)
    {
        if (damage.Empty || !uid.HasValue)
            return damage.Empty ? damage : null;

        var before = new BeforeDamageChangedEvent(damage, origin, canBeCancelled);
        RaiseLocalEvent(uid.Value, ref before);

        if (before.Cancelled)
            return null;

        if (!_damageableQuery.Resolve(uid.Value, ref damageable, false))
            return null;

        damage = ApplyUniversalAllModifiers(damage);
        damage *= partMultiplier;

        if (!ignoreResistances)
        {
            if (damageable.DamageModifierSetId != null &&
                _prototypeManager.TryIndex(damageable.DamageModifierSetId, out var modifierSet))
            {
                damage = DamageSpecifier.ApplyModifierSet(damage, modifierSet);
            }

            var ev = new DamageModifyEvent(damage, origin, targetPart);
            RaiseLocalEvent(uid.Value, ev);
            damage = ev.Damage;

            if (damage.Empty)
                return damage;
        }

        // backmen edit start
        var specialHandlerEvent = new HandleCustomDamage(
            damage,
            targetPart,
            canBeCancelled,
            origin);

        RaiseLocalEvent(uid.Value, ref specialHandlerEvent);

        if (specialHandlerEvent.Handled)
            return specialHandlerEvent.Damage;
        // backmen edit end

        var delta = new DamageSpecifier();
        delta.DamageDict.EnsureCapacity(damage.DamageDict.Count);

        var dict = damageable.Damage.DamageDict;
        foreach (var (type, value) in damage.DamageDict)
        {
            if (!dict.TryGetValue(type, out var oldValue))
                continue;

            var newValue = FixedPoint2.Max(FixedPoint2.Zero, oldValue + value);
            if (newValue == oldValue)
                continue;

            dict[type] = newValue;
            delta.DamageDict[type] = newValue - oldValue;
        }

        if (delta.DamageDict.Count > 0)
            OnEntityDamageChanged((uid.Value, damageable), delta, interruptsDoAfters, origin);

        return delta;
    }

    public DamageSpecifier ApplyUniversalAllModifiers(DamageSpecifier damage)
    {
        if (UniversalAllDamageModifier == 1f && UniversalAllHealModifier == 1f)
            return damage;

        foreach (var (key, value) in damage.DamageDict)
        {
            if (value == 0)
                continue;

            if (value > 0)
                damage.DamageDict[key] *= UniversalAllDamageModifier;
            else
                damage.DamageDict[key] *= UniversalAllHealModifier;
        }

        return damage;
    }

    public void SetAllDamage(Entity<DamageableComponent> ent, FixedPoint2 newValue)
    {
        if (newValue < 0)
            return;

        foreach (var type in ent.Comp.Damage.DamageDict.Keys.ToList())
        {
            ent.Comp.Damage.DamageDict[type] = newValue;
        }

        OnEntityDamageChanged(ent, new DamageSpecifier());

        // backmen edit start
        // Синхронизация severity ран с общим уроном (для rejuvenate и т.п.)
        if (TryComp<BodyComponent>(ent, out var body) && TryComp<ConsciousnessComponent>(ent, out _))
        {
            foreach (var (part, _) in _body.GetBodyChildren(ent.Owner, body))
            {
                if (!TryComp(part, out WoundableComponent? woundable))
                    continue;

                foreach (var wound in _wounds.GetWoundableWounds(part, woundable))
                {
                    _wounds.SetWoundSeverity(wound, newValue, wound);
                }
            }
        }
        // backmen edit end
    }

    public void SetDamageModifierSetId(EntityUid uid, string? damageModifierSetId, DamageableComponent? comp = null)
    {
        if (!_damageableQuery.Resolve(uid, ref comp))
            return;

        comp.DamageModifierSetId = damageModifierSetId;
        Dirty(uid, comp);
    }

    private void DamageableGetState(Entity<DamageableComponent> ent, ref ComponentGetState args)
    {
        if (_netMan.IsServer)
        {
            args.State = new DamageableComponentState(
                ent.Comp.Damage.DamageDict,
                ent.Comp.DamageContainerID,
                ent.Comp.DamageModifierSetId,
                ent.Comp.HealthBarThreshold);
            return;
        }

        args.State = new DamageableComponentState(
            ent.Comp.Damage.DamageDict.ShallowClone(),
            ent.Comp.DamageContainerID,
            ent.Comp.DamageModifierSetId,
            ent.Comp.HealthBarThreshold);
    }

    private void OnIrradiated(EntityUid uid, DamageableComponent component, OnIrradiatedEvent args)
    {
        var damageValue = FixedPoint2.New(args.TotalRads);
        var damage = new DamageSpecifier();

        foreach (var typeId in component.RadiationDamageTypeIDs)
        {
            damage.DamageDict.Add(typeId, damageValue);
        }

        TryChangeDamage(uid, damage, interruptsDoAfters: false, origin: args.Origin);
    }

    private void OnRejuvenate(EntityUid uid, DamageableComponent component, RejuvenateEvent args)
    {
        if (TryComp<MobThresholdsComponent>(uid, out var thresholds))
        {
            _mobThreshold.SetAllowRevives(uid, true, thresholds);
        }

        SetAllDamage((uid, component), 0);

        if (TryComp<MobThresholdsComponent>(uid, out thresholds))
        {
            _mobThreshold.SetAllowRevives(uid, false, thresholds);
        }
    }

    private void DamageableHandleState(Entity<DamageableComponent> ent, ref ComponentHandleState args)
    {
        if (args.Current is not DamageableComponentState state)
            return;

        ent.Comp.DamageContainerID = state.DamageContainerId;
        ent.Comp.DamageModifierSetId = state.ModifierSetId;
        ent.Comp.HealthBarThreshold = state.HealthBarThreshold;

        var newDamage = new DamageSpecifier { DamageDict = new(state.DamageDict) };
        var delta = newDamage - ent.Comp.Damage;
        delta.TrimZeros();

        if (delta.Empty)
            return;

        ent.Comp.Damage = newDamage;
        OnEntityDamageChanged(ent, delta);
    }
}
