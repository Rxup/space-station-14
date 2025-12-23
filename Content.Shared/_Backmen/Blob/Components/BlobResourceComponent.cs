using Content.Shared.FixedPoint;

namespace Content.Shared._Backmen.Blob.Components;

[RegisterComponent]
public sealed partial class BlobResourceComponent : Component
{
    [DataField]
    public FixedPoint2 PointsPerPulsed = 3;
}
