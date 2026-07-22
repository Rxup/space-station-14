using System;
using Robust.Shared.Configuration;

namespace Content.Shared.Backmen.CCVar;

public sealed partial class CCVars
{
    /*
     * Medical CVars
     */

    /// <summary>
    /// How many times per second do we want to heal wounds.
    /// </summary>
    public static readonly CVarDef<float> MedicalHealingTickrate =
        CVarDef.Create("medical.heal_tickrate", 0.5f, CVar.SERVERONLY);

    /// <summary>
    /// Deprecated: wound severity is no longer capped; kept for legacy server configs.
    /// </summary>
    [Obsolete("Wound severity is no longer capped at this value.")]
    public static readonly CVarDef<float> MaxWoundSeverity =
        CVarDef.Create("wounding.max_wound_severity", 200f, CVar.SERVERONLY);

    /// <summary>
    /// The same as above
    /// </summary>
    public static readonly CVarDef<float> WoundScarChance =
        CVarDef.Create("wounding.wound_scar_chance", 0.10f, CVar.SERVERONLY);

    /// <summary>
    /// How much of the wound's severity will be transferred?
    /// </summary>
    public static readonly CVarDef<float> WoundTransferPart =
        CVarDef.Create("wounding.wound_severity_transfer", 0.10f, CVar.SERVER | CVar.REPLICATED);

    /// <summary>
    /// Per how much damage, the prototype value of "wound chance" is counted
    /// For example, a wound with merge chance of 0.1, with a merge ratio of 10, and severity of 10
    /// Would have a merge chance of 0.1, cuz, (severity / merge ratio) * merge chance
    /// </summary>
    public static readonly CVarDef<float> WoundMergeRatio =
        CVarDef.Create("wounding.wound_merge_ratio", 10f, CVar.SERVER | CVar.REPLICATED);

    /// <summary>
    /// the said x amount of percents
    /// </summary>
    public static readonly CVarDef<float> DodgeDistanceChange =
        CVarDef.Create("targeting.dodge_change_distance", 0.08f, CVar.SERVER | CVar.REPLICATED);

    /*
     * Pain CVars
     */

    /// <summary>
    /// Should the Pain System work at all?
    /// </summary>
    public static readonly CVarDef<bool> PainEnabled =
        CVarDef.Create("pain.enabled", true, CVar.SERVER | CVar.REPLICATED);

    /// <summary>
    /// Default volume setting of the brutal death rattles and pain screams
    /// </summary>
    public static readonly CVarDef<float> BrutalDeathRattlesVolume =
        CVarDef.Create("pain.volume", 0.5f, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// Should Pain System trigger pain reflexes?
    /// </summary>
    public static readonly CVarDef<bool> PainReflexesEnabled =
        CVarDef.Create("pain.reflexes_enabled", true, CVar.SERVER | CVar.REPLICATED);

    /// <summary>
    /// The global pain multiplier, applied to every pain source.
    /// </summary>
    public static readonly CVarDef<float> UniversalPainMultiplier =
        CVarDef.Create("pain.universal_multiplier", 1f, CVar.SERVER | CVar.REPLICATED);

    /// <summary>
    /// How much pain can a single pain inflicter induce?
    /// </summary>
    public static readonly CVarDef<float> PainInflicterCapacity =
        CVarDef.Create("pain.inflicter_cap", 100f, CVar.SERVER | CVar.REPLICATED);

    /*
     * Trauma CVars
     */

    /// <summary>
    /// The multiplier applied to the base paralyze time upon an infliction of organ trauma.
    /// </summary>
    public static readonly CVarDef<float> OrganTraumaSlowdownTimeMultiplier =
        CVarDef.Create("traumas.organ_slowdown_time", 2f, CVar.SERVER | CVar.REPLICATED);

    /// <summary>
    /// The slowdown applied to the walk speed upon an infliction of organ trauma
    /// </summary>
    public static readonly CVarDef<float> OrganTraumaWalkSpeedSlowdown =
        CVarDef.Create("traumas.organ_walk_speed_slowdown", 0.6f, CVar.SERVER | CVar.REPLICATED);

    /// <summary>
    /// The slowdown applied to the run speed upon an infliction of organ trauma
    /// </summary>
    public static readonly CVarDef<float> OrganTraumaRunSpeedSlowdown =
        CVarDef.Create("traumas.organ_run_speed_slowdown", 0.6f, CVar.SERVER | CVar.REPLICATED);

    /// <summary>
    /// The pain feels threshold on a specific nerve, that if broken, will not allow inducing another nerve damage trauma
    /// </summary>
    public static readonly CVarDef<float> NerveDamageThreshold =
        CVarDef.Create("traumas.nerve_damage_threshold", 0.7f, CVar.SERVER | CVar.REPLICATED);

    /// <summary>
    /// The chance that will be rolled for adding a trauma to player's lungs in case of CPR failure.
    /// </summary>
    public static readonly CVarDef<float> CprTraumaChance =
        CVarDef.Create("traumas.cpr_trauma_chance", 0.1f, CVar.SERVERONLY);

    /*
     * Bleeding CVars
     */

    /// <summary>
    /// The rate at which severity (wound) points get exchanged into bleeding; e.g., 50 severity would be 11 bleeding points.
    /// </summary>
    public static readonly CVarDef<float> BleedingSeverityTrade =
        CVarDef.Create("bleeds.wound_severity_trade", 0.12f, CVar.SERVERONLY);

    /// <summary>
    /// How long by default do bleeds grow to their full form?
    /// </summary>
    public static readonly CVarDef<float> BleedsScalingTime =
        CVarDef.Create("bleeds.bleeding_scaling_time", 2f, CVar.SERVERONLY);
}
