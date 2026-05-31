using Content.Shared.Fluids;

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
    /// Minimum puddle fill (fraction of <see cref="PuddleComponent.OverflowVolume"/>) to leave footprints or drain liquid.
    /// Matches the puddle sprite / slip threshold by default.
    /// </summary>
    [DataField]
    public float MinVolumeRatio = SharedPuddleSystem.LowThreshold;
}
