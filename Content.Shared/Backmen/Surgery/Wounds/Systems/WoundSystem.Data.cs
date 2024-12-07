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
        { WoundSeverity.Loss, 150 },
    };

    private readonly Dictionary<WoundableSeverity, FixedPoint2> _woundableThresholds = new()
    {
        { WoundableSeverity.Minor, 1 },
        { WoundableSeverity.Moderate, 25 },
        { WoundableSeverity.Severe, 50 },
        { WoundableSeverity.Critical, 80 },
        { WoundableSeverity.Loss, 150 },
    };

    #endregion
}
