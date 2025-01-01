using Content.Shared.FixedPoint;

namespace Content.Shared.Backmen.Surgery.Wounds.Systems;

public partial class WoundSystem
{
    #region Data

    private readonly Dictionary<WoundSeverity, FixedPoint2> _woundThresholds = new()
    {
        { WoundSeverity.Healed, 0 },
        { WoundSeverity.Minor, 1 },
        { WoundSeverity.Moderate, 25 },
        { WoundSeverity.Severe, 50 },
        { WoundSeverity.Critical, 80 },
        { WoundSeverity.Loss, 100 },
    };

    private readonly Dictionary<WoundableSeverity, FixedPoint2> _thresholdMultipliers = new()
    {
        { WoundableSeverity.Minor, 1 },
        { WoundableSeverity.Moderate, 1 },
        { WoundableSeverity.Severe, 1.2 },
        { WoundableSeverity.Critical, 1.4 },
        // TODO: Would be great if someone actually playing the game balanced this out,
        // because I haven't touched SS14 in like a year now, but this value is to make
        // limb loss more like to appear when a person gets critted
        { WoundableSeverity.Loss, 2 }, // lmao no limb
    };

    #endregion
}
