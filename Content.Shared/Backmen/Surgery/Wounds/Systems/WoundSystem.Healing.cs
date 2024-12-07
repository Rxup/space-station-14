using Content.Shared.Backmen.Surgery.CCVar;
using Content.Shared.Backmen.Surgery.Wounds.Components;
using Content.Shared.FixedPoint;

namespace Content.Shared.Backmen.Surgery.Wounds.Systems;

public partial class WoundSystem
{
    private void UpdateHealing(float frameTime)
    {
        var wounds = EntityQueryEnumerator<WoundComponent>();
        var healRate = 1 / _cfg.GetCVar(SurgeryCvars.MedicalHealingTickrate);
        var woundsToModify = new List<(EntityUid uid, WoundComponent wound)>();

        while (wounds.MoveNext(out var uid, out var wound))
        {
            if (!wound.CanBeHealed)
                continue;

            wound.AccumulatedFrameTime += frameTime;
            if (wound.AccumulatedFrameTime < healRate)
                continue;

            wound.AccumulatedFrameTime -= healRate;

            woundsToModify.Add((uid, wound));
        }

        foreach (var (uid, wound) in woundsToModify)
        {
            ApplyWoundSeverity(uid, -wound.BaseHealingRate, wound,true);
            CheckForMinusSeverity(uid, wound);
            CheckSeverityThresholds(uid, wound);
        }

        // That's it! o(( >ω< ))o
    }

    #region Public API

    /// <summary>
    /// Applies base healing rate to wounds.
    /// </summary>
    /// <param name="uid">UID of the wound.</param>
    /// <param name="change">Number to add.</param>
    /// <param name="wound">Wound to which healing is applied.</param>
    public void ApplyWoundHealingRate(EntityUid uid, FixedPoint2 change, WoundComponent? wound)
    {
        if (!Resolve(uid, ref wound) || _net.IsClient)
            return;

        wound.BaseHealingRate += ApplyModifiersToHealRate(wound, change);

        CheckSeverityThresholds(uid, wound);
        Dirty(uid, wound);
    }

    /// <summary>
    /// Sets base healing rate to wounds.
    /// </summary>
    /// <param name="uid">UID of the wound.</param>
    /// <param name="change">Number to set.</param>
    /// <param name="wound">Wound to which healing is applied.</param>
    public void SetWoundHealingRate(EntityUid uid, FixedPoint2 change, WoundComponent? wound)
    {
        if (!Resolve(uid, ref wound) || _net.IsClient)
            return;

        wound.BaseHealingRate = ApplyModifiersToHealRate(wound, change);

        CheckSeverityThresholds(uid, wound);
        Dirty(uid, wound);
    }

    /// <summary>
    /// Applies healing multiplier to wounds.
    /// </summary>
    /// <param name="uid">UID of the wound.</param>
    /// <param name="change">Healing multiplier.</param>
    /// <param name="identifier">Identifier for multiplier.</param>
    /// <param name="wound">Wound to which healing multiplier is applied.</param>
    public bool ApplyHealingRateMultiplier(EntityUid uid, FixedPoint2 change, string identifier, WoundComponent? wound)
    {
        if (!Resolve(uid, ref wound) || _net.IsClient)
            return false;

        wound.HealingMultipliers.Add((uid, wound), new HealingMultiplier(change, identifier));

        ApplyModifiersToHealRate(wound, wound.BaseHealingRate);
        CheckSeverityThresholds(uid, wound);

        Dirty(uid, wound);
        return true;
    }

    /// <summary>
    /// Applies healing multiplier to wounds.
    /// </summary>
    /// <param name="uid">UID of the wound.</param>
    /// <param name="identifier">Identifier for multiplier.</param>
    /// <param name="wound">Wound to which healing multiplier is applied.</param>
    public bool RemoveHealingRateMultiplier(EntityUid uid, string identifier, WoundComponent? wound)
    {
        if (!Resolve(uid, ref wound) || _net.IsClient)
            return false;

        if (!wound.HealingMultipliers.Remove((uid, wound), out _))
            return false;

        ApplyModifiersToHealRate(wound, wound.BaseHealingRate);
        CheckSeverityThresholds(uid, wound);

        Dirty(uid, wound);
        return true;
    }

    #endregion

    #region Private API

    private FixedPoint2 ApplyModifiersToHealRate(WoundComponent wound, FixedPoint2 change)
    {
        if (wound.HealingMultipliers.Count == 0)
            return change;

        var healRateMultiplier = 0;
        foreach (var (_, value) in wound.HealingMultipliers)
        {
            healRateMultiplier += (int) value.Change;
        }

        healRateMultiplier /= wound.HealingMultipliers.Count;
        return change * healRateMultiplier;
    }

    private void CheckForMinusSeverity(EntityUid uid, WoundComponent? wound = null)
    {
        if (TerminatingOrDeleted(uid))
            return;

        if (!Resolve(uid, ref wound))
            return;

        if (wound.WoundSeverityPoint < 0)
            wound.WoundSeverityPoint = 0;
    }

    #endregion
}
