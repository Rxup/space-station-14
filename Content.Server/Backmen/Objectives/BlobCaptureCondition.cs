using Content.Server.Backmen.Blob.Components;

namespace Content.Server.Backmen.Objectives;

[RegisterComponent]
public sealed partial class BlobCaptureConditionComponent : Component
{
    [DataField("target")] public int Target { get; set; } = StationBlobConfigComponent.DefaultStageEnd;
}
