namespace Content.Server.Humanoid.Components;

[RegisterComponent]
public sealed partial class RandomHumanoidProfileComponent : Component
{
    [DataField]
    public bool RandomizeName = true;
}
