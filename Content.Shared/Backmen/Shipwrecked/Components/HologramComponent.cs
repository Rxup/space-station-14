using Robust.Shared.GameStates;

namespace Content.Shared.Backmen.Shipwrecked.Components;

/// <summary>
/// Marks an entity as a hologram. Client <c>HologramVisualizerSystem</c> applies the holopad scanline shader.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class HologramComponent : Component
{
    /// <summary>Shader prototype id (same as holopad projections).</summary>
    [DataField]
    public string ShaderName = "Hologram";

    [DataField]
    public Color Color1 = Color.FromHex("#65b8e2");

    [DataField]
    public Color Color2 = Color.FromHex("#3a6981");

    [DataField]
    public float Alpha = 0.9f;

    [DataField]
    public float Intensity = 2f;

    [DataField]
    public float ScrollRate = 0.125f;
}
