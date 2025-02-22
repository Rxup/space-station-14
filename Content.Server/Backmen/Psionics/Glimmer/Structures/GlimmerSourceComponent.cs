namespace Content.Server.Backmen.Psionics.Glimmer;

[RegisterComponent]
/// <summary>
/// Adds to glimmer at regular intervals. We'll use it for glimmer drains too when we get there.
/// </summary>
public sealed partial class GlimmerSourceComponent : Component
{
    [DataField("accumulator"), ViewVariables(VVAccess.ReadWrite)]
    public float Accumulator = 0f;

    [DataField("active")]
    public bool Active = true;

    /// <summary>
    ///     Since glimmer is an int, we'll do it like this.
    /// </summary>
    [DataField("secondsPerGlimmer"), ViewVariables(VVAccess.ReadWrite)]
    public float SecondsPerGlimmer = 10f;

    /// <summary>
    ///     True if it produces glimmer, false if it subtracts it.
    /// </summary>
    [DataField("addToGlimmer")]
    public bool AddToGlimmer = true;

    [DataField("range")]
    public float Range = 4f;

}
