using Robust.Shared.GameStates;

namespace Content.Shared._Lavaland.Shuttles.Components;

/// <summary>
/// Marker component for the mining shuttle grid.
/// Used for lavaland's FTL whitelist.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class MiningShuttleComponent : Component
{
    /// <summary>
    /// Power draw multiplier for devices that were not present on this shuttle at mapinit.
    /// </summary>
    [DataField]
    public float ExtraPowerMultiplier = 10f;
}
