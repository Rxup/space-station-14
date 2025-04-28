using Content.Shared.Backmen.CCVar;
using Content.Shared.Backmen.Surgery.Traumas.Components;
using Content.Shared.Backmen.Surgery.Wounds;
using Content.Shared.Backmen.Surgery.Wounds.Components;
using Content.Shared.Backmen.Surgery.Wounds.Systems;
using Content.Shared.FixedPoint;
using JetBrains.Annotations;
using Robust.Shared.Configuration;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Shared.Backmen.Surgery;

[UsedImplicitly]
public abstract class SharedBloodstreamSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _gameTiming = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly WoundSystem _wound = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BleedInflicterComponent, WoundSeverityPointChangedEvent>(OnWoundSeverityUpdate);

        SubscribeLocalEvent<BleedInflicterComponent, WoundHealAttemptEvent>(OnWoundHealAttempt);
        SubscribeLocalEvent<BleedInflicterComponent, WoundAddedEvent>(OnWoundAdded);
    }

    /// <summary>
    /// Add a bleed-ability modifier on woundable
    /// </summary>
    /// <param name="woundable">Entity uid of the woundable to apply the modifiers</param>
    /// <param name="identifier">string identifier of the modifier</param>
    /// <param name="priority">Priority of the said modifier</param>
    /// <param name="canBleed">Should the wounds bleed?</param>
    /// <param name="force">If forced, won't stop after failing to apply one modifier</param>
    /// <param name="woundableComp">Woundable Component</param>
    /// <returns>Return true if applied</returns>
    public bool TryAddBleedModifier(
        EntityUid woundable,
        string identifier,
        int priority,
        bool canBleed,
        bool force = false,
        WoundableComponent? woundableComp = null)
    {
        if (!Resolve(woundable, ref woundableComp))
            return false;

        foreach (var woundEnt in _wound.GetWoundableWounds(woundable, woundableComp))
        {
            if (!TryComp<BleedInflicterComponent>(woundEnt, out var bleedsComp))
                continue;

            if (TryAddBleedModifier(woundEnt, identifier, priority, canBleed, bleedsComp))
                continue;

            if (!force)
                return false;
        }

        return true;
    }

    /// <summary>
    /// Add a bleed-ability modifier
    /// </summary>
    /// <param name="uid">Entity uid of the wound</param>
    /// <param name="identifier">string identifier of the modifier</param>
    /// <param name="priority">Priority of the said modifier</param>
    /// <param name="canBleed">Should the wound bleed?</param>
    /// <param name="comp">Bleed Inflicter Component</param>
    /// <returns>Return true if applied</returns>
    public bool TryAddBleedModifier(
        EntityUid uid,
        string identifier,
        int priority,
        bool canBleed,
        BleedInflicterComponent? comp = null)
    {
        if (!Resolve(uid, ref comp))
            return false;

        if (!comp.BleedingModifiers.TryAdd(identifier, (priority, canBleed)))
            return false;

        Dirty(uid, comp);
        return true;
    }

    /// <summary>
    /// Remove a bleed-ability modifier from a woundable
    /// </summary>
    /// <param name="uid">Entity uid of the woundable</param>
    /// <param name="identifier">string identifier of the modifier</param>
    /// <param name="force">If forced, won't stop applying modifiers after failing one wound</param>
    /// <param name="woundable">Woundable Component</param>
    /// <returns>Returns true if removed all modifiers ON WOUNDABLE</returns>
    public bool TryRemoveBleedModifier(
        EntityUid uid,
        string identifier,
        bool force = false,
        WoundableComponent? woundable = null)
    {
        if (!Resolve(uid, ref woundable))
            return false;

        foreach (var woundEnt in _wound.GetWoundableWounds(uid, woundable))
        {
            if (!TryComp<BleedInflicterComponent>(woundEnt, out var bleedsComp))
                continue;

            if (TryRemoveBleedModifier(woundEnt, identifier, bleedsComp))
                continue;

            if (!force)
                return false;
        }

        return true;
    }

    /// <summary>
    /// Remove a bleed-ability modifier
    /// </summary>
    /// <param name="uid">Entity uid of the wound</param>
    /// <param name="identifier">string identifier of the modifier</param>
    /// <param name="comp">Bleed Inflicter Component</param>
    /// <returns>Return true if removed</returns>
    public bool TryRemoveBleedModifier(
        EntityUid uid,
        string identifier,
        BleedInflicterComponent? comp = null)
    {
        if (!Resolve(uid, ref comp))
            return false;

        if (!comp.BleedingModifiers.Remove(identifier))
            return false;

        Dirty(uid, comp);
        return true;
    }

    /// <summary>
    /// Redact a modifiers meta data
    /// </summary>
    /// <param name="wound">The wound entity uid</param>
    /// <param name="identifier">Identifier of the modifier</param>
    /// <param name="priority">Priority to set</param>
    /// <param name="canBleed">Should it bleed?</param>
    /// <param name="bleeds">Bleed Inflicter Component</param>
    /// <returns>true if was changed</returns>
    public bool ChangeBleedsModifierMetadata(
        EntityUid wound,
        string identifier,
        int priority,
        bool? canBleed,
        BleedInflicterComponent? bleeds = null)
    {
        if (!Resolve(wound, ref bleeds))
            return false;

        if (!bleeds.BleedingModifiers.TryGetValue(identifier, out var pair))
            return false;

        bleeds.BleedingModifiers[identifier] = (Priority: priority, CanBleed: canBleed ?? pair.CanBleed);
        return true;
    }

    /// <summary>
    /// Redact a modifiers meta data
    /// </summary>
    /// <param name="wound">The wound entity uid</param>
    /// <param name="identifier">Identifier of the modifier</param>
    /// <param name="priority">Priority to set</param>
    /// <param name="canBleed">Should it bleed?</param>
    /// <param name="bleeds">Bleed Inflicter Component</param>
    /// <returns>true if was changed</returns>
    public bool ChangeBleedsModifierMetadata(
        EntityUid wound,
        string identifier,
        bool canBleed,
        int? priority,
        BleedInflicterComponent? bleeds = null)
    {
        if (!Resolve(wound, ref bleeds))
            return false;

        if (!bleeds.BleedingModifiers.TryGetValue(identifier, out var pair))
            return false;

        bleeds.BleedingModifiers[identifier] = (Priority: priority ?? pair.Priority, CanBleed: canBleed);
        return true;
    }

    /// <summary>
    /// Self-explanatory
    /// </summary>
    /// <param name="uid">Wound entity</param>
    /// <param name="comp">Bleeds Inflicter Component </param>
    /// <returns>Returns whether if the wound can bleed</returns>
    public bool CanWoundBleed(EntityUid uid, BleedInflicterComponent? comp = null)
    {
        if (!Resolve(uid, ref comp))
            return false;

        var nearestModifier = comp.BleedingModifiers.FirstOrNull();
        if (nearestModifier == null)
            return true; // No modifiers. return true

        var lastCanBleed = true;
        var lastPriority = 0;
        foreach (var (_, pair) in comp.BleedingModifiers)
        {
            if (pair.Priority <= lastPriority)
                continue;

            lastPriority = pair.Priority;
            lastCanBleed = pair.CanBleed;
        }

        return lastCanBleed;
    }

    private void OnWoundHealAttempt(EntityUid uid, BleedInflicterComponent component, ref WoundHealAttemptEvent args)
    {
        if (component.IsBleeding)
            args.Cancelled = true;
    }

    private void OnWoundAdded(EntityUid uid, BleedInflicterComponent component, ref WoundAddedEvent args)
    {
        if (!CanWoundBleed(uid, component) || args.Component.WoundSeverityPoint < component.SeverityThreshold)
            return;

        // wounds that BLEED will not HEAL.
        component.BleedingAmountRaw = args.Component.WoundSeverityPoint * _cfg.GetCVar(CCVars.BleedingSeverityTrade);

        var formula = (float) (args.Component.WoundSeverityPoint / _cfg.GetCVar(CCVars.BleedsScalingTime) * component.ScalingSpeed);
        component.ScalingFinishesAt = _gameTiming.CurTime + TimeSpan.FromSeconds(formula);
        component.ScalingStartsAt = _gameTiming.CurTime;

        component.IsBleeding = true;

        Dirty(uid, component);
    }

    private void OnWoundSeverityUpdate(EntityUid uid,
        BleedInflicterComponent component,
        ref WoundSeverityPointChangedEvent args)
    {
        if (!CanWoundBleed(uid, component) || args.NewSeverity < component.SeverityThreshold)
            return;

        var oldBleedsAmount = args.OldSeverity * _cfg.GetCVar(CCVars.BleedingSeverityTrade);
        component.BleedingAmountRaw = args.NewSeverity * _cfg.GetCVar(CCVars.BleedingSeverityTrade);

        var severityPenalty = component.BleedingAmountRaw - oldBleedsAmount / _cfg.GetCVar(CCVars.BleedsScalingTime);
        component.SeverityPenalty += severityPenalty;

        var formula = (float) (args.Component.WoundSeverityPoint / _cfg.GetCVar(CCVars.BleedsScalingTime) * component.ScalingSpeed);
        component.ScalingFinishesAt = _gameTiming.CurTime + TimeSpan.FromSeconds(formula);
        component.ScalingStartsAt = _gameTiming.CurTime;

        if (!component.IsBleeding && args.NewSeverity > args.OldSeverity)
        {
            component.ScalingLimit += 0.6;
            component.IsBleeding = true;
            // When bleeding is reopened, the severity is increased
        }

        Dirty(uid, component);
    }
}
