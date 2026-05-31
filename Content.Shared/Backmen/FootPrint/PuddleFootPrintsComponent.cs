using Content.Shared.FixedPoint;

namespace Content.Shared.Backmen.FootPrint;

[RegisterComponent]
public sealed partial class PuddleFootPrintsComponent : Component
{
    [DataField]
    public float SizeRatio = 0.2f;

    /// <summary>
    /// Puddles with more than this percent of water do not leave footprints.
    /// </summary>
    [DataField]
    public float OffPercent = 80f;

    /// <summary>
    /// Minimum solution volume in a puddle before footprints are left or liquid is consumed.
    /// </summary>
    [DataField]
    public FixedPoint2 MinVolume = 1.01f;
}
