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
        {BlobTileType.Node, null}, // Convenient for events.
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

/// <summary>
/// Event raised on all special tiles of Blob Node on pulse.
/// </summary>
public sealed class BlobSpecialGetPulseEvent : EntityEventArgs;