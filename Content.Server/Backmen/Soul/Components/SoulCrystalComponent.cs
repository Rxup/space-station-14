namespace Content.Server.Backmen.Soul;

[RegisterComponent]
public sealed partial class SoulCrystalComponent : Component
{
    /// <summary>
    /// Basically, the identity of the soul inside this entity.
    /// </summary>
    [DataField("trueName")]
    public string? TrueName;
}
