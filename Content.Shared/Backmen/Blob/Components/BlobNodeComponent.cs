namespace Content.Shared.Backmen.Blob.Components;

[RegisterComponent]
public sealed partial class BlobNodeComponent : Component
{
    [ViewVariables(VVAccess.ReadWrite), DataField("pulseFrequency")]
    public float PulseFrequency = 4;

    [ViewVariables(VVAccess.ReadWrite), DataField("pulseRadius")]
    public float PulseRadius = 3f;

    public TimeSpan NextPulse = TimeSpan.Zero;

    /// <summary>
    /// All tiles that are connected to that blob node.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    public HashSet<EntityUid> ConnectedTiles = [];

    [ViewVariables(VVAccess.ReadWrite)]
    public EntityUid? ResourceBlob { get; set; }

    [ViewVariables(VVAccess.ReadWrite)]
    public EntityUid? FactoryBlob { get; set; }
}

public sealed class BlobTileGetPulseEvent : EntityEventArgs
{
    public bool Explain { get; set; }
}

public sealed class BlobMobGetPulseEvent : EntityEventArgs;
