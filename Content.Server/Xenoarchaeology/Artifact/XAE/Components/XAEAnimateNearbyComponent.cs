namespace Content.Server.Xenoarchaeology.Artifact.XAE.Components;

/// <summary>
/// Animates nearby items when the artifact node activates.
/// </summary>
[RegisterComponent, Access(typeof(XAEAnimateNearbySystem))]
public sealed partial class XAEAnimateNearbyComponent : Component
{
    [DataField]
    public float Range = 4f;

    [DataField]
    public TimeSpan Duration = TimeSpan.FromSeconds(30);
}
