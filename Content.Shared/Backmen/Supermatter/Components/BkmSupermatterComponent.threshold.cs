namespace Content.Shared.Backmen.Supermatter.Components;

public partial class BkmSupermatterComponent
{
    /// <summary>
    /// Higher == Higher percentage of inhibitor gas needed
    /// before the charge inertia chain reaction effect starts.
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly)]
    [DataField("powerlossinhibitiongasThreshold")]
    public float PowerlossInhibitionGasThreshold = 0.20f;

    /// <summary>
    /// Higher == More moles of the gas are needed before the charge
    /// inertia chain reaction effect starts.
    /// Scales powerloss inhibition down until this amount of moles is reached
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly)]
    [DataField("powerlossinhibitionmoleThreshold")]
    public float PowerlossInhibitionMoleThreshold = 20f;

    /// <summary>
    /// bonus powerloss inhibition boost if this amount of moles is reached
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly)]
    [DataField("powerlossinhibitionmoleboostThreshold")]
    public float PowerlossInhibitionMoleBoostThreshold = 500f;

    /// <summary>
    /// Above this value we can get lord singulo and independent mol damage,
    /// below it we can heal damage
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly)]
    [DataField("molepenaltyThreshold")]
    public float MolePenaltyThreshold = 1800f;

    /// <summary>
    /// more moles of gases are harder to heat than fewer,
    /// so let's scale heat damage around them
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly)]
    [DataField("moleheatpenaltyThreshold")]
    public float MoleHeatPenaltyThreshold;

    /// <summary>
    /// The cutoff on power properly doing damage, pulling shit around,
    /// and delamming into a tesla. Low chance of pyro anomalies, +2 bolts of electricity
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly)]
    [DataField("powerPenaltyThreshold")]
    public float PowerPenaltyThreshold = 5000f;

    /// <summary>
    /// Maximum safe operational temperature in degrees Celsius. Supermatter begins taking damage above this temperature.
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly)]
    [DataField("heatpenaltyThreshold")]
    public float HeatPenaltyThreshold = 40f;

    /// <summary>
    /// The damage we had before this cycle. Used to limit the damage we can take each cycle, and for safe alert
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    public float DamageArchived = 0f;

    /// <summary>
    /// is multiplied by ExplosionPoint to cap
    /// evironmental damage per cycle
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly)]
    public float DamageHardcap = 0.002f;

    /// <summary>
    /// environmental damage is scaled by this
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly)]
    [DataField("damageincreaseMultiplier")]
    public float DamageIncreaseMultiplier = 0.25f;

    /// <summary>
    /// if spaced sm wont take more than 2 damage per cycle
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly)]
    [DataField("maxspaceexposureDamage")]
    public float MaxSpaceExposureDamage = 2;

    /// <summary>
    ///     The point at which we should start sending radio messages about the damage.
    /// </summary>
    [DataField]
    public float DamageWarningThreshold = 50;

    /// <summary>
    ///     The point at which we start sending station announcements about the damage.
    /// </summary>
    [DataField]
    public float DamageEmergencyThreshold = 500;

    /// <summary>
    ///     The point at which the SM begins delaminating.
    /// </summary>
    [DataField]
    public int DamageDelaminationPoint = 900;
}
