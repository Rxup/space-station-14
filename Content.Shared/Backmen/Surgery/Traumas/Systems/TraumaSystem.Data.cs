using Content.Shared.Backmen.Surgery.Wounds;
using Content.Shared.FixedPoint;

namespace Content.Shared.Backmen.Surgery.Traumas.Systems;

public partial class TraumaSystem
{
    #region Data

    private readonly Dictionary<BoneSeverity, FixedPoint2> _boneThresholds = new()
    {
        { BoneSeverity.Normal, 40 },
        { BoneSeverity.Damaged, 20 },
        { BoneSeverity.Broken, 0 },
    };

    private readonly Dictionary<BoneSeverity, FixedPoint2> _bonePainModifiers = new()
    {
        { BoneSeverity.Normal, 0.5 },
        { BoneSeverity.Damaged, 0.75 },
        { BoneSeverity.Broken, 1 },
    };

    private readonly Dictionary<WoundableSeverity, FixedPoint2> _boneTraumaChanceMultipliers = new()
    {
        { WoundableSeverity.Healthy, 0 },
        { WoundableSeverity.Minor, 0.06 },
        { WoundableSeverity.Moderate, 0.14 },
        { WoundableSeverity.Severe, 0.24 },
        { WoundableSeverity.Critical, 0.45 },
        { WoundableSeverity.Loss, 0.45 },
    };

    private readonly Dictionary<WoundableSeverity, FixedPoint2> _boneDamageMultipliers = new()
    {
        { WoundableSeverity.Healthy, 0 },
        { WoundableSeverity.Minor, 0.4 },
        { WoundableSeverity.Moderate, 0.6 },
        { WoundableSeverity.Severe, 0.9 },
        { WoundableSeverity.Critical, 1.25 },
        { WoundableSeverity.Loss, 1.6 }, // Fun.
    };

    #endregion
}
