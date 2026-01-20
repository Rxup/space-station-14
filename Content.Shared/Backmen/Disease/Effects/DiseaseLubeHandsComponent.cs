using Robust.Shared.GameStates;

namespace Content.Shared.Backmen.Disease.Effects;

/// <summary>
/// Component that applies lube to items when touched
/// </summary>
[RegisterComponent]
[NetworkedComponent]
public sealed partial class DiseaseLubeHandsComponent : Component
{
    /// <summary>
    /// Chance to apply lube when touching an item (0-1)
    /// </summary>
    [DataField]
    public float LubeChance = 0.3f;

    /// <summary>
    /// Number of slips the lubed item will have
    /// </summary>
    [DataField]
    public int Slips = 3;

    /// <summary>
    /// Slip strength
    /// </summary>
    [DataField]
    public int SlipStrength = 2;
}
