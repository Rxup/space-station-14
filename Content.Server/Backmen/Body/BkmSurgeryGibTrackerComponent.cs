using Content.Shared.FixedPoint;

namespace Content.Server.Backmen.Body;

/// <summary>
/// Tracks total damage dealt to a surgery patient for full-body gib threshold checks.
/// </summary>
[RegisterComponent]
public sealed partial class BkmSurgeryGibTrackerComponent : Component
{
    public FixedPoint2 AccumulatedDamage;
}
