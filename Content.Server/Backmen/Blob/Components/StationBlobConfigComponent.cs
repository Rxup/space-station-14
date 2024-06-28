namespace Content.Server.Backmen.Blob.Components;

public sealed partial class StationBlobConfigComponent : Component
{
    [ViewVariables(VVAccess.ReadWrite), DataField("blobTilesDetect")]
    public int BlobTilesDetect = 30;

    [ViewVariables(VVAccess.ReadWrite), DataField("blobTilesCritical")]
    public int BlobTilesCritical = 400;

    [ViewVariables(VVAccess.ReadWrite), DataField("blobTilesWin")]
    public int BlobTilesWin = 800;

    [ViewVariables(VVAccess.ReadWrite), DataField("specForceAmount")]
    public int SpecForceAmount = 6;
}
