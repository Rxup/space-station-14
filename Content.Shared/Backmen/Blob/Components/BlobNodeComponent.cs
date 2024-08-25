namespace Content.Shared.Backmen.Blob.Components;

[RegisterComponent]
public sealed partial class BlobNodeComponent : Component
{
    [DataField]
    public float PulseFrequency = 4f;

    [DataField]
    public float PulseRadius = 3f;

    public TimeSpan NextPulse = TimeSpan.Zero;

    [DataField]
    public Dictionary<BlobTileType, EntityUid?> ConnectedTiles = new()
    {
        {BlobTileType.Resource, null},
        {BlobTileType.Factory, null},
        {BlobTileType.Storage, null},
        {BlobTileType.Turret, null},
    };
}

public sealed class BlobTileGetPulseEvent : EntityEventArgs
{
    public bool Explain { get; set; }
}

public sealed class BlobMobGetPulseEvent : EntityEventArgs;
