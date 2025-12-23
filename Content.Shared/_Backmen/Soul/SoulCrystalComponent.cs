using Robust.Shared.GameStates;

namespace Content.Shared._Backmen.Soul;

[RegisterComponent,NetworkedComponent]
public sealed partial class SoulCrystalComponent : Component
{
    /// <summary>
    /// Basically, the identity of the soul inside this entity.
    /// </summary>
    [DataField("trueName")]
    public string? TrueName;
}
