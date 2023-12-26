using Content.Shared.Alert;
using Content.Shared.Backmen.Species.Shadowkin.Components;
using Content.Shared.Backmen.Species.Shadowkin.Events;
using System.Threading.Tasks;
using Robust.Shared.Timing;

namespace Content.Server.Backmen.Species.Shadowkin.Systems;

public sealed class ShadowkinPowerSystem : EntitySystem
{
    [Dependency] private readonly AlertsSystem _alerts = default!;

    private readonly Dictionary<ShadowkinPowerThreshold, string> _powerDictionary = new();

    private ISawmill _sawmill = default!;


    public override void Initialize()
    {
        base.Initialize();

        _powerDictionary.Add(ShadowkinPowerThreshold.Max, Loc.GetString("shadowkin-power-max"));
        _powerDictionary.Add(ShadowkinPowerThreshold.Great, Loc.GetString("shadowkin-power-great"));
        _powerDictionary.Add(ShadowkinPowerThreshold.Good, Loc.GetString("shadowkin-power-good"));
        _powerDictionary.Add(ShadowkinPowerThreshold.Okay, Loc.GetString("shadowkin-power-okay"));
        _powerDictionary.Add(ShadowkinPowerThreshold.Tired, Loc.GetString("shadowkin-power-tired"));
        _powerDictionary.Add(ShadowkinPowerThreshold.Min, Loc.GetString("shadowkin-power-min"));

        _sawmill = Logger.GetSawmill("ShadowkinPowerSystem");
    }

    /// <param name="powerLevel">The current power level.</param>
    /// <returns>The name of the power level.</returns>
    public string GetLevelName(float powerLevel)
    {
        // Placeholders
        var result = ShadowkinPowerThreshold.Min;
        var value = ShadowkinComponent.PowerThresholds[ShadowkinPowerThreshold.Max];

        // Find the highest threshold that is lower than the current power level
        foreach (var threshold in ShadowkinComponent.PowerThresholds)
        {
            if (threshold.Value <= value &&
                threshold.Value >= powerLevel)
            {
                result = threshold.Key;
                value = threshold.Value;
            }
        }

        // Return the name of the threshold
        _powerDictionary.TryGetValue(result, out var powerType);
        powerType ??= Loc.GetString("shadowkin-power-okay");
        return powerType;
    }

    /// <summary>
    ///    Sets the alert level of a shadowkin.
    /// </summary>
    /// <param name="uid">The entity uid.</param>
    /// <param name="enabled">Enable the alert or not</param>
    /// <param name="powerLevel">The current power level.</param>
    public void UpdateAlert(EntityUid uid, bool enabled, float? powerLevel = null)
    {
        if (!enabled || powerLevel == null)
        {
            _alerts.ClearAlert(uid, AlertType.ShadowkinPower);
            return;
        }

        // Get shadowkin component
        if (!TryComp<ShadowkinComponent>(uid, out var component))
        {
            _sawmill.Error("Tried to update alert of entity without shadowkin component.");
            return;
        }

        // 250 / 7 ~= 35
        // Pwr / 35 ~= (0-7)
        // Round to ensure (0-7)
        var power = Math.Clamp(Math.Round(component.PowerLevel / 35), 0, 7);

        // Set the alert level
        _alerts.ShowAlert(uid, AlertType.ShadowkinPower, (short) power);
    }


    /// <summary>
    ///     Tries to update the power level of a shadowkin based on an amount of seconds.
    /// </summary>
    /// <param name="uid">The entity uid.</param>
    /// <param name="frameTime">The time since the last update in seconds.</param>
    public bool TryUpdatePowerLevel(EntityUid uid, float frameTime)
    {
        // Check if the entity has a shadowkin component
        if (!TryComp<ShadowkinComponent>(uid, out var component))
            return false;

        // Check if power gain is enabled
        if (!component.PowerLevelGainEnabled)
            return false;

        // Set the new power level
        UpdatePowerLevel(uid, frameTime);

        return true;
    }

    /// <summary>
    ///     Updates the power level of a shadowkin based on an amount of seconds.
    /// </summary>
    /// <param name="uid">The entity uid.</param>
    /// <param name="frameTime">The time since the last update in seconds.</param>
    public void UpdatePowerLevel(EntityUid uid, float frameTime)
    {
        // Get shadowkin component
        if (!TryComp<ShadowkinComponent>(uid, out var component))
        {
            _sawmill.Error("Tried to update power level of entity without shadowkin component.");
            return;
        }

        // Calculate new power level (P = P + t * G * M)
        var newPowerLevel = component.PowerLevel + frameTime * component.PowerLevelGain * component.PowerLevelGainMultiplier;

        // Clamp power level using clamp function
        newPowerLevel = Math.Clamp(newPowerLevel, component.PowerLevelMin, component.PowerLevelMax);

        // Set the new power level
        SetPowerLevel(uid, newPowerLevel);
    }


    /// <summary>
    ///     Tries to add to the power level of a shadowkin.
    /// </summary>
    /// <param name="uid">The entity uid.</param>
    /// <param name="amount">The amount to add to the power level.</param>
    public bool TryAddPowerLevel(EntityUid uid, float amount)
    {
        // Check if the entity has a shadowkin component
        if (!TryComp<ShadowkinComponent>(uid, out _))
            return false;

        // Set the new power level
        AddPowerLevel(uid, amount);

        return true;
    }

    /// <summary>
    ///     Adds to the power level of a shadowkin.
    /// </summary>
    /// <param name="uid">The entity uid.</param>
    /// <param name="amount">The amount to add to the power level.</param>
    public void AddPowerLevel(EntityUid uid, float amount)
    {
        // Get shadowkin component
        if (!TryComp<ShadowkinComponent>(uid, out var component))
        {
            _sawmill.Error("Tried to add to power level of entity without shadowkin component.");
            return;
        }

        // Get new power level
        var newPowerLevel = component.PowerLevel + amount;

        // Clamp power level using clamp function
        newPowerLevel = Math.Clamp(newPowerLevel, component.PowerLevelMin, component.PowerLevelMax);

        // Set the new power level
        SetPowerLevel(uid, newPowerLevel);
    }


    /// <summary>
    ///     Sets the power level of a shadowkin.
    /// </summary>
    /// <param name="uid">The entity uid.</param>
    /// <param name="newPowerLevel">The new power level.</param>
    public void SetPowerLevel(EntityUid uid, float newPowerLevel)
    {
        // Get shadowkin component
        if (!TryComp<ShadowkinComponent>(uid, out var component))
        {
            _sawmill.Error("Tried to set power level of entity without shadowkin component.");
            return;
        }

        // Clamp power level using clamp function
        newPowerLevel = Math.Clamp(newPowerLevel, component.PowerLevelMin, component.PowerLevelMax);

        // Set the new power level
        component._powerLevel = newPowerLevel;
    }


    /// <summary>
    ///     Tries to blackeye a shadowkin.
    /// </summary>
    public bool TryBlackeye(EntityUid uid)
    {
        // Raise an attempted blackeye event
        var ev = new ShadowkinBlackeyeAttemptEvent(GetNetEntity(uid));
        RaiseLocalEvent(ev);
        if (ev.Cancelled)
            return false;

        Blackeye(uid);
        return true;
    }

    /// <summary>
    ///     Blackeyes a shadowkin.
    /// </summary>
    public void Blackeye(EntityUid uid)
    {
        // Get shadowkin component
        if (!TryComp<ShadowkinComponent>(uid, out var component))
        {
            _sawmill.Error("Tried to blackeye entity without shadowkin component.");
            return;
        }

        component.Blackeye = true;
        var ent = GetNetEntity(uid);
        RaiseNetworkEvent(new ShadowkinBlackeyeEvent(ent));
        RaiseLocalEvent(new ShadowkinBlackeyeEvent(ent));
    }


    /// <summary>
    ///     Tries to add a power multiplier.
    /// </summary>
    /// <param name="uid">The entity uid.</param>
    /// <param name="multiplier">The multiplier to add.</param>
    /// <param name="time">The time in seconds to wait before removing the multiplier.</param>
    public bool TryAddMultiplier(EntityUid uid, float multiplier, float? time = null)
    {
        if (!HasComp<ShadowkinComponent>(uid) ||
            float.IsNaN(multiplier))
            return false;

        AddMultiplier(uid, multiplier, time);

        return true;
    }

    /// <summary>
    ///     Adds a power multiplier.
    /// </summary>
    /// <param name="uid">The entity uid.</param>
    /// <param name="multiplier">The multiplier to add.</param>
    /// <param name="time">The time in seconds to wait before removing the multiplier.</param>
    public void AddMultiplier(EntityUid uid, float multiplier, float? time = null)
    {
        // Get shadowkin component
        if (!TryComp<ShadowkinComponent>(uid, out var component))
        {
            _sawmill.Error("Tried to add multiplier to entity without shadowkin component.");
            return;
        }

        // Add the multiplier
        component.PowerLevelGainMultiplier += multiplier;

        Dirty(uid, component);

        // Remove the multiplier after a certain amount of time
        if (time != null)
        {
            Timer.Spawn((int)time * 1000, () =>
            {
                if(!uid.IsValid())
                    return;
                component.PowerLevelGainMultiplier -= multiplier;
                Dirty(uid, component);
            });
        }
    }
}
