namespace Content.Server.Backmen.Research.SophicScribe;

[RegisterComponent]
public sealed partial class SophicScribeComponent : Component
{
    [DataField("accumulator"), ViewVariables(VVAccess.ReadWrite)]
    public float Accumulator = 0f;

    [DataField("announceInterval")]
    public TimeSpan AnnounceInterval = TimeSpan.FromMinutes(2);

    [DataField("nextAnnounce")]
    public TimeSpan NextAnnounceTime = default!;

    /// <summary>
    ///     Antispam.
    /// </summary>
    public TimeSpan StateTime = default!;

    [DataField("stateCD"), ViewVariables]
    public TimeSpan StateCD = TimeSpan.FromSeconds(5);
}
