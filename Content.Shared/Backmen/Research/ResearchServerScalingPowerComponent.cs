using Content.Shared.Atmos;

namespace Content.Shared.Backmen.Research;

/// <summary>
/// Scales an R&amp;D server's APC load with the number of unlocked technologies.
/// </summary>
[RegisterComponent]
public sealed partial class ResearchServerScalingPowerComponent : Component
{
    [DataField]
    public float BaseLoad = 15000f;

    [DataField]
    public float LoadPerTechnology = 100f;

    /// <summary>
    /// Roundstart technologies do not add extra load.
    /// </summary>
    [DataField]
    public bool IgnoreRoundstart = true;

    /// <summary>
    /// Surrounding air must not exceed this temperature (Kelvin) for the server to operate.
    /// Default is 120 °C.
    /// </summary>
    [DataField]
    public float MaxTemperature = Atmospherics.T0C + 120f;
}
