using Content.Server._Backmen.Blob.Components;

namespace Content.Server._Backmen.Objectives;

[RegisterComponent]
public sealed partial class BlobCaptureConditionComponent : Component
{
    [DataField]
    public int Target { get; set; } = StationBlobConfigComponent.DefaultStageEnd;
}
