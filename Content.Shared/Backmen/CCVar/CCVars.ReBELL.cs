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
    /// The name is self-explanatory
    /// </summary>
    public static readonly CVarDef<float> MaxWoundSeverity =
        CVarDef.Create("wounding.max_wound_severity", 200f, CVar.SERVERONLY);

    /// <summary>
    /// The same as above
    /// </summary>
    public static readonly CVarDef<float> WoundScarChance =
        CVarDef.Create("wounding.wound_scar_chance", 0.10f, CVar.SERVERONLY);

    /// <summary>
    /// What part of wounds will be transferred from a destroyed woundable to its parent?
    /// </summary>
    public static readonly CVarDef<float> WoundTransferPart =
        CVarDef.Create("wounding.wound_severity_transfer", 0.10f, CVar.SERVER);

    /// <summary>
    /// for every n units of distance, (tiles), chance for dodging is equal to n*x percents, look for it down here
    /// </summary>
    public static readonly CVarDef<float> DodgeDistanceChance =
        CVarDef.Create("targeting.dodge_chance_distance", 4f, CVar.SERVER);

    /// <summary>
    /// the said x amount of percents
    /// </summary>
    public static readonly CVarDef<float> DodgeDistanceChange =
        CVarDef.Create("targeting.dodge_change_distance", 0.05f, CVar.SERVER);

    /*
     * Trauma CVars
     */

    /// <summary>
    /// The multiplier applied to the base paralyze time upon an infliction of organ trauma.
    /// </summary>
    public static readonly CVarDef<float> OrganTraumaSlowdownTimeMultiplier =
        CVarDef.Create("traumas.organ_slowdown_time", 2f, CVar.SERVERONLY);

    /// <summary>
    /// The slowdown applied to the walk speed upon an infliction of organ trauma
    /// </summary>
    public static readonly CVarDef<float> OrganTraumaWalkSpeedSlowdown =
        CVarDef.Create("traumas.organ_walk_speed_slowdown", 0.6f, CVar.SERVERONLY);

    /// <summary>
    /// The slowdown applied to the run speed upon an infliction of organ trauma
    /// </summary>
    public static readonly CVarDef<float> OrganTraumaRunSpeedSlowdown =
        CVarDef.Create("traumas.organ_run_speed_slowdown", 0.6f, CVar.SERVERONLY);

    /*
     * Bleeding CVars
     */

    /// <summary>
    /// The rate at which severity (wound) points get exchanged into bleeding; e.g., 50 severity would be 7.5 bleeding points.
    /// </summary>
    public static readonly CVarDef<float> BleedingSeverityTrade =
        CVarDef.Create("bleeds.wound_severity_trade", 0.15f, CVar.SERVERONLY);

    /// <summary>
    /// How quick by default do bleeds grow to their full form?
    /// </summary>
    public static readonly CVarDef<float> BleedsScalingTime =
        CVarDef.Create("bleeds.bleeding_scaling_time", 9f, CVar.SERVERONLY);
}
