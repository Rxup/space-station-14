using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Shared.Alert;
using Content.Shared.Backmen.Surgery.Consciousness;
using Content.Shared.Backmen.Surgery.Consciousness.Components;
using Content.Shared.Backmen.Surgery.Wounds.Components;
using Content.Shared.Backmen.Surgery.Wounds.Systems;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Events;
using Robust.Shared.GameStates;
using Robust.Shared.Network;
using Robust.Shared.Serialization;

namespace Content.Shared.Mobs.Systems;

public sealed class MobThresholdSystem : EntitySystem
{
    [Dependency] private readonly MobStateSystem _mobStateSystem = default!;
    [Dependency] private readonly AlertsSystem _alerts = default!;

    // backmen edit start
    [Dependency] private readonly SharedBodySystem _body = default!;

    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly WoundSystem _wound = default!;
    // backmen edit end

    public override void Initialize()
    {
        SubscribeLocalEvent<MobThresholdsComponent, ComponentGetState>(OnGetState);
        SubscribeLocalEvent<MobThresholdsComponent, ComponentHandleState>(OnHandleState);

        SubscribeLocalEvent<MobThresholdsComponent, ComponentShutdown>(MobThresholdShutdown);
        SubscribeLocalEvent<MobThresholdsComponent, ComponentStartup>(MobThresholdStartup);
        SubscribeLocalEvent<MobThresholdsComponent, DamageChangedEvent>(OnDamaged);
        SubscribeLocalEvent<MobThresholdsComponent, ConsciousnessChangedEvent>(OnConsciousnessUpdate); // backmen edit
        SubscribeLocalEvent<MobThresholdsComponent, UpdateMobStateEvent>(OnUpdateMobState);
        SubscribeLocalEvent<MobThresholdsComponent, MobStateChangedEvent>(OnThresholdsMobState);
    }

    private void OnGetState(EntityUid uid, MobThresholdsComponent component, ref ComponentGetState args)
    {
        var thresholds = new Dictionary<FixedPoint2, MobState>();
        foreach (var (key, value) in component.Thresholds)
        {
            thresholds.Add(key, value);
        }
        args.State = new MobThresholdsComponentState(thresholds,
            component.TriggersAlerts,
            component.CurrentThresholdState,
            component.StateAlertDict,
            component.ShowOverlays,
            component.AllowRevives);
    }

    private void OnHandleState(EntityUid uid, MobThresholdsComponent component, ref ComponentHandleState args)
    {
        if (args.Current is not MobThresholdsComponentState state)
            return;
        component.Thresholds = new SortedDictionary<FixedPoint2, MobState>(state.UnsortedThresholds);
        component.TriggersAlerts = state.TriggersAlerts;
        component.CurrentThresholdState = state.CurrentThresholdState;
        component.AllowRevives = state.AllowRevives;
    }

    #region Public API

    /// <summary>
    /// Gets the next available state for a mob.
    /// </summary>
    /// <param name="target">Target entity</param>
    /// <param name="mobState">Supplied MobState</param>
    /// <param name="nextState">The following MobState. Can be null if there isn't one.</param>
    /// <param name="thresholdsComponent">Threshold Component Owned by the target</param>
    /// <returns>True if the next mob state exists</returns>
    public bool TryGetNextState(
        EntityUid target,
        MobState mobState,
        [NotNullWhen(true)] out MobState? nextState,
        MobThresholdsComponent? thresholdsComponent = null)
    {
        nextState = null;
        if (!Resolve(target, ref thresholdsComponent))
            return false;

        MobState? min = null;
        foreach (var state in thresholdsComponent.Thresholds.Values)
        {
            if (state <= mobState)
                continue;

            if (min == null || state < min)
                min = state;
        }

        nextState = min;
        return nextState != null;
    }

    /// <summary>
    /// Get the Damage Threshold for the appropriate state if it exists
    /// </summary>
    /// <param name="target">Target Entity</param>
    /// <param name="mobState">MobState we want the Damage Threshold of</param>
    /// <param name="thresholdComponent">Threshold Component Owned by the target</param>
    /// <returns>the threshold or 0 if it doesn't exist</returns>
    public FixedPoint2 GetThresholdForState(EntityUid target, MobState mobState,
        MobThresholdsComponent? thresholdComponent = null)
    {
        if (!Resolve(target, ref thresholdComponent))
            return FixedPoint2.Zero;

        foreach (var pair in thresholdComponent.Thresholds)
        {
            if (pair.Value == mobState)
            {
                return pair.Key;
            }
        }

        return FixedPoint2.Zero;
    }

    /// <summary>
    /// Try to get the Damage Threshold for the appropriate state if it exists
    /// </summary>
    /// <param name="target">Target Entity</param>
    /// <param name="mobState">MobState we want the Damage Threshold of</param>
    /// <param name="threshold">The damage Threshold for the given state</param>
    /// <param name="thresholdComponent">Threshold Component Owned by the target</param>
    /// <returns>true if successfully retrieved a threshold</returns>
    public bool TryGetThresholdForState(EntityUid target, MobState mobState,
        [NotNullWhen(true)] out FixedPoint2? threshold,
        MobThresholdsComponent? thresholdComponent = null)
    {
        threshold = null;
        if (!Resolve(target, ref thresholdComponent))
            return false;

        foreach (var pair in thresholdComponent.Thresholds)
        {
            if (pair.Value == mobState)
            {
                threshold = pair.Key;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Try to get the a percentage of the Damage Threshold for the appropriate state if it exists
    /// </summary>
    /// <param name="target">Target Entity</param>
    /// <param name="mobState">MobState we want the Damage Threshold of</param>
    /// <param name="damage">The Damage being applied</param>
    /// <param name="percentage">Percentage of Damage compared to the Threshold</param>
    /// <param name="thresholdComponent">Threshold Component Owned by the target</param>
    /// <returns>true if successfully retrieved a percentage</returns>
    public bool TryGetPercentageForState(EntityUid target, MobState mobState, FixedPoint2 damage,
        [NotNullWhen(true)] out FixedPoint2? percentage,
        MobThresholdsComponent? thresholdComponent = null)
    {
        percentage = null;
        if (!TryGetThresholdForState(target, mobState, out var threshold, thresholdComponent))
            return false;

        percentage = damage / threshold;
        return true;
    }

    /// <summary>
    /// Try to get the Damage Threshold for crit or death. Outputs the first found threshold.
    /// </summary>
    /// <param name="target">Target Entity</param>
    /// <param name="threshold">The Damage Threshold for incapacitation</param>
    /// <param name="thresholdComponent">Threshold Component owned by the target</param>
    /// <returns>true if successfully retrieved incapacitation threshold</returns>
    public bool TryGetIncapThreshold(EntityUid target, [NotNullWhen(true)] out FixedPoint2? threshold,
        MobThresholdsComponent? thresholdComponent = null)
    {
        threshold = null;
        if (!Resolve(target, ref thresholdComponent))
            return false;

        return TryGetThresholdForState(target, MobState.Critical, out threshold, thresholdComponent)
               || TryGetThresholdForState(target, MobState.Dead, out threshold, thresholdComponent);
    }

    /// <summary>
    /// Try to get a percentage of the Damage Threshold for crit or death. Outputs the first found percentage.
    /// </summary>
    /// <param name="target">Target Entity</param>
    /// <param name="damage">The damage being applied</param>
    /// <param name="percentage">Percentage of Damage compared to the Incapacitation Threshold</param>
    /// <param name="thresholdComponent">Threshold Component Owned by the target</param>
    /// <returns>true if successfully retrieved incapacitation percentage</returns>
    public bool TryGetIncapPercentage(EntityUid target, FixedPoint2 damage,
        [NotNullWhen(true)] out FixedPoint2? percentage,
        MobThresholdsComponent? thresholdComponent = null)
    {
        percentage = null;
        if (!TryGetIncapThreshold(target, out var threshold, thresholdComponent))
            return false;

        if (damage == 0)
        {
            percentage = 0;
            return true;
        }

        percentage = FixedPoint2.Min(1.0f, damage / threshold.Value);
        return true;
    }

    /// <summary>
    /// Try to get the Damage Threshold for death
    /// </summary>
    /// <param name="target">Target Entity</param>
    /// <param name="threshold">The Damage Threshold for death</param>
    /// <param name="thresholdComponent">Threshold Component owned by the target</param>
    /// <returns>true if successfully retrieved incapacitation threshold</returns>
    public bool TryGetDeadThreshold(EntityUid target, [NotNullWhen(true)] out FixedPoint2? threshold,
        MobThresholdsComponent? thresholdComponent = null)
    {
        threshold = null;
        if (!Resolve(target, ref thresholdComponent, false))
            return false;

        return TryGetThresholdForState(target, MobState.Dead, out threshold, thresholdComponent);
    }

    /// <summary>
    /// Try to get a percentage of the Damage Threshold for death
    /// </summary>
    /// <param name="target">Target Entity</param>
    /// <param name="damage">The damage being applied</param>
    /// <param name="percentage">Percentage of Damage compared to the Death Threshold</param>
    /// <param name="thresholdComponent">Threshold Component Owned by the target</param>
    /// <returns>true if successfully retrieved death percentage</returns>
    public bool TryGetDeadPercentage(EntityUid target, FixedPoint2 damage,
        [NotNullWhen(true)] out FixedPoint2? percentage,
        MobThresholdsComponent? thresholdComponent = null)
    {
        percentage = null;
        if (!TryGetDeadThreshold(target, out var threshold, thresholdComponent))
            return false;

        if (damage == 0)
        {
            percentage = 0;
            return true;
        }

        percentage = FixedPoint2.Min(1.0f, damage / threshold.Value);
        return true;
    }

    /// <summary>
    /// Takes the damage from one entity and scales it relative to the health of another
    /// </summary>
    /// <param name="target1">The entity whose damage will be scaled</param>
    /// <param name="target2">The entity whose health the damage will scale to</param>
    /// <param name="damage">The newly scaled damage. Can be null</param>
    public bool GetScaledDamage(EntityUid target1, EntityUid target2, out DamageSpecifier? damage)
    {
        damage = null;

        if (!TryComp<DamageableComponent>(target1, out var oldDamage))
            return false;

        // backmen ediT!!!!!
        var entDamage = oldDamage.Damage;
        if (TryComp<BodyComponent>(target1, out var body) && HasComp<ConsciousnessComponent>(target1))
        {
            var damageDict = new Dictionary<string, FixedPoint2>();
            foreach (var bodyPart in _body.GetBodyChildren(target1, body))
            {
                if (!TryComp<WoundableComponent>(bodyPart.Id, out var woundable))
                    continue;

                foreach (var woundEnt in _wound.GetWoundableWounds(bodyPart.Id, woundable))
                {
                    if (!damageDict.TryAdd(woundEnt.Comp.DamageType, woundEnt.Comp.WoundSeverityPoint))
                    {
                        damageDict[woundEnt.Comp.DamageType] = woundEnt.Comp.WoundSeverityPoint;
                    }
                }
            }

            foreach (var damagePiece in damageDict)
            {
                entDamage.DamageDict[damagePiece.Key] += damagePiece.Value;
            }
        }
        // backmen eidT1!!11!

        if (!TryComp<MobThresholdsComponent>(target1, out var threshold1) ||
            !TryComp<MobThresholdsComponent>(target2, out var threshold2))
            return false;

        if (!TryGetThresholdForState(target1, MobState.Dead, out var ent1DeadThreshold, threshold1))
            ent1DeadThreshold = 0;

        if (!TryGetThresholdForState(target2, MobState.Dead, out var ent2DeadThreshold, threshold2))
            ent2DeadThreshold = 0;

        damage = (entDamage / ent1DeadThreshold.Value) * ent2DeadThreshold.Value;

        return true;
    }

    /// <summary>
    /// Set a MobState Threshold or create a new one if it doesn't exist
    /// </summary>
    /// <param name="target">Target Entity</param>
    /// <param name="damage">Damageable Component owned by the target</param>
    /// <param name="mobState">MobState Component owned by the target</param>
    /// <param name="threshold">MobThreshold Component owned by the target</param>
    public void SetMobStateThreshold(EntityUid target, FixedPoint2 damage, MobState mobState,
        MobThresholdsComponent? threshold = null)
    {
        if (!Resolve(target, ref threshold))
            return;

        // create a duplicate dictionary so we don't modify while enumerating.
        var thresholds = new Dictionary<FixedPoint2, MobState>(threshold.Thresholds);
        foreach (var (damageThreshold, state) in thresholds)
        {
            if (state != mobState)
                continue;
            threshold.Thresholds.Remove(damageThreshold);
        }
        threshold.Thresholds[damage] = mobState;
        Dirty(target, threshold);
        VerifyThresholds(target, threshold);
    }

    /// <summary>
    /// Checks to see if we should change states based on thresholds.
    /// Call this if you change the amount of damagable without triggering a damageChangedEvent or if you change
    /// </summary>
    /// <param name="target">Target Entity</param>
    /// <param name="threshold">Threshold Component owned by the Target</param>
    /// <param name="mobState">MobState Component owned by the Target</param>
    /// <param name="damageable">Damageable Component owned by the Target</param>
    /// <param name="body">Body Component owned by the Target</param>
    public void VerifyThresholds(EntityUid target, MobThresholdsComponent? threshold = null,
        MobStateComponent? mobState = null, DamageableComponent? damageable = null, BodyComponent? body = null)
    {
        if (!Resolve(target, ref mobState, ref threshold, false))
            return;

        if (damageable != null)
            CheckThresholds(target, mobState, threshold, damageable);
        if (_net.IsServer)
        {
            // backmen edit
            RaiseNetworkEvent(new MobThresholdChecked(GetNetEntity(target)), target);
        }

        UpdateAlerts(target, mobState.CurrentState, threshold, damageable, body);
    }

    public void SetAllowRevives(EntityUid uid, bool val, MobThresholdsComponent? component = null)
    {
        if (!Resolve(uid, ref component, false))
            return;
        component.AllowRevives = val;
        Dirty(uid, component);
        VerifyThresholds(uid, component);
    }

    #endregion

    #region Private Implementation

    private void CheckThresholds(EntityUid target, MobStateComponent mobStateComponent,
        MobThresholdsComponent thresholdsComponent, DamageableComponent damageableComponent, EntityUid? origin = null)
    {
        foreach (var (threshold, mobState) in thresholdsComponent.Thresholds.Reverse())
        {
            if (damageableComponent.TotalDamage < threshold)
                continue;

            TriggerThreshold(target, mobState, mobStateComponent, thresholdsComponent, origin);
            break;
        }
    }

    private void TriggerThreshold(
        EntityUid target,
        MobState newState,
        MobStateComponent? mobState = null,
        MobThresholdsComponent? thresholds = null,
        EntityUid? origin = null)
    {
        if (!Resolve(target, ref mobState, ref thresholds) ||
            mobState.CurrentState == newState)
        {
            return;
        }

        if (mobState.CurrentState != MobState.Dead || thresholds.AllowRevives)
        {
            thresholds.CurrentThresholdState = newState;
            Dirty(target, thresholds);
        }

        _mobStateSystem.UpdateMobState(target, mobState, origin);
    }

    private void UpdateAlerts(EntityUid target, MobState currentMobState, MobThresholdsComponent? threshold = null,
        DamageableComponent? damageable = null, BodyComponent? body = null)
    {
        if (!Resolve(target, ref threshold))
            return;

        // don't handle alerts if they are managed by another system... BobbySim (soon TM)
        if (!threshold.TriggersAlerts)
            return;

        if (!threshold.StateAlertDict.TryGetValue(currentMobState, out var currentAlert))
        {
            Log.Error($"No alert alert for mob state {currentMobState} for entity {ToPrettyString(target)}");
            return;
        }

        if (!_alerts.TryGet(currentAlert, out var alertPrototype))
        {
            Log.Error($"Invalid alert type {currentAlert}");
            return;
        }

        if (alertPrototype.SupportsSeverity)
        {
            // backmen edit start
            var totalDamage = FixedPoint2.Zero;
            if (damageable != null && !HasComp<ConsciousnessComponent>(target))
            {
                totalDamage = damageable.TotalDamage;
            }
            else if (TryComp<ConsciousnessComponent>(target, out var consciousness))
            {
                totalDamage = consciousness.Cap - consciousness.Consciousness;
            }
            // backmen edit end

            var severity = _alerts.GetMinSeverity(currentAlert);

            var ev = new BeforeAlertSeverityCheckEvent(currentAlert, severity);
            RaiseLocalEvent(target, ev);

            if (ev.CancelUpdate)
            {
                _alerts.ShowAlert(target, ev.CurrentAlert, ev.Severity);
                return;
            }

            if (TryGetNextState(target, currentMobState, out var nextState, threshold) &&
                TryGetPercentageForState(target, nextState.Value, totalDamage, out var percentage))
            {
                percentage = FixedPoint2.Clamp(percentage.Value, 0, 1);

                severity = (short) MathF.Round(
                    MathHelper.Lerp(
                        _alerts.GetMinSeverity(currentAlert),
                        _alerts.GetMaxSeverity(currentAlert),
                        percentage.Value.Float()));
            }
            _alerts.ShowAlert(target, currentAlert, severity);
        }
        else
        {
            _alerts.ShowAlert(target, currentAlert);
        }
    }

    private void OnDamaged(EntityUid target, MobThresholdsComponent thresholds, DamageChangedEvent args)
    {
        if (!TryComp<MobStateComponent>(target, out var mobState))
            return;
        CheckThresholds(target, mobState, thresholds, args.Damageable, args.Origin);

        if (_net.IsServer)
        {
            RaiseNetworkEvent(new MobThresholdChecked(GetNetEntity(target)), target);
        }

        UpdateAlerts(target, mobState.CurrentState, thresholds, args.Damageable);
    }

    private void OnConsciousnessUpdate(EntityUid body, MobThresholdsComponent thresholds, ref ConsciousnessChangedEvent args)
    {
        if (!TryComp<MobStateComponent>(body, out var mobState))
            return;

        // mob states are handled by consciousness. so we fine here
        if (_net.IsServer)
        {
            RaiseNetworkEvent(new MobThresholdChecked(GetNetEntity(body)), body);
        }

        UpdateAlerts(body, mobState.CurrentState, thresholds, null, Comp<BodyComponent>(body));
    }

    private void MobThresholdStartup(EntityUid target, MobThresholdsComponent thresholds, ComponentStartup args)
    {
        if (!TryComp<MobStateComponent>(target, out var mobState) || !TryComp<DamageableComponent>(target, out var damageable))
            return;
        CheckThresholds(target, mobState, thresholds, damageable);
        UpdateAllEffects((target, thresholds, mobState, damageable), mobState.CurrentState);
    }

    private void MobThresholdShutdown(EntityUid target, MobThresholdsComponent component, ComponentShutdown args)
    {
        if (component.TriggersAlerts)
            _alerts.ClearAlertCategory(target, component.HealthAlertCategory);
    }

    private void OnUpdateMobState(EntityUid target, MobThresholdsComponent component, ref UpdateMobStateEvent args)
    {
        if (!component.AllowRevives && component.CurrentThresholdState == MobState.Dead)
        {
            args.State = MobState.Dead;
        }
        else if (component.CurrentThresholdState != MobState.Invalid)
        {
            args.State = component.CurrentThresholdState;
        }
    }

    private void UpdateAllEffects(Entity<MobThresholdsComponent, MobStateComponent?, DamageableComponent?, BodyComponent?> ent, MobState currentState)
    {
        var (_, thresholds, mobState, damageable, bodyComponent) = ent;
        if (Resolve(ent, ref thresholds, ref mobState) && _net.IsServer)
        {
            RaiseNetworkEvent(new MobThresholdChecked(GetNetEntity(ent.Owner)), ent.Owner);
        }

        UpdateAlerts(ent, currentState, thresholds, damageable, bodyComponent);
    }

    private void OnThresholdsMobState(Entity<MobThresholdsComponent> ent, ref MobStateChangedEvent args)
    {
        UpdateAllEffects((ent, ent, null, null), args.NewMobState);
    }

    #endregion
}

/// <summary>
/// Event that triggers when an entity with a mob threshold is checked
/// </summary>
[Serializable, NetSerializable]
public sealed class MobThresholdChecked : EntityEventArgs
{
    public NetEntity Uid { get; }
    public MobThresholdChecked(NetEntity uid)
    {
        Uid = uid;
    }
}


