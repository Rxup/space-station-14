using Robust.Shared.Configuration;

namespace Content.Shared._DV.CCVars;

/// <summary>
/// DeltaV specific cvars.
/// </summary>
[CVarDefs]
// ReSharper disable once InconsistentNaming - Shush you
public sealed class DCCVars
{
    /*
     * Cosmic Cult
     */
    /// <summary>
    /// How much entropy a convert is worth towards the next monument tier.
    /// </summary>
    public static readonly CVarDef<int> CosmicCultistEntropyValue =
        CVarDef.Create("cosmiccult.cultist_entropy_value", 7, CVar.SERVER);

    /// <summary>
    /// How much of the crew the cult is aiming to convert for a tier 3 monument.
    /// </summary>
    public static readonly CVarDef<int> CosmicCultTargetConversionPercent =
        CVarDef.Create("cosmiccult.target_conversion_percent", 40, CVar.SERVER);

    /// <summary>
    /// How long the timer for the cult's stewardship vote lasts.
    /// </summary>
    public static readonly CVarDef<int> CosmicCultStewardVoteTimer =
        CVarDef.Create("cosmiccult.steward_vote_timer", 30, CVar.SERVER);

    /// <summary>
    /// The delay between the monument getting upgraded to tier 2 and the crew learning of that fact. the monument cannot be upgraded again in this time.
    /// </summary>
    public static readonly CVarDef<int> CosmicCultT2RevealDelaySeconds =
        CVarDef.Create("cosmiccult.t2_reveal_delay_seconds", 120, CVar.SERVER);

    /// <summary>
    /// The delay between the monument getting upgraded to tier 3 and the crew learning of that fact. the monument cannot be upgraded again in this time.
    /// </summary>
    public static readonly CVarDef<int> CosmicCultT3RevealDelaySeconds =
        CVarDef.Create("cosmiccult.t3_reveal_delay_seconds", 60, CVar.SERVER);

    /// <summary>
    /// The delay between the monument getting upgraded to tier 3 and the finale starting.
    /// </summary>
    public static readonly CVarDef<int> CosmicCultFinaleDelaySeconds =
        CVarDef.Create("cosmiccult.extra_entropy_for_finale", 150, CVar.SERVER);
}