namespace Content.Server.Backmen.Blob;

[RegisterComponent]
public sealed partial class BlobNodeComponent : Component
{
    [ViewVariables(VVAccess.ReadWrite), DataField("pulseFrequency")]
    public float PulseFrequency = 4;

    [ViewVariables(VVAccess.ReadWrite), DataField("pulseRadius")]
    public float PulseRadius = 3f;

    public TimeSpan NextPulse = TimeSpan.Zero;
}

public sealed class BlobTileGetPulseEvent : EntityEventArgs
{
    public bool Explain { get; set; }
}

public sealed class BlobMobGetPulseEvent : EntityEventArgs
{
}
