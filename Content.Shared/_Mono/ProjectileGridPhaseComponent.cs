using Robust.Shared.GameStates;

namespace Content.Shared._Mono;

/// <summary>
/// Marker component for projectiles that should phase through (ignore collisions with) entities on the same grid.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ProjectileGridPhaseComponent : Component
{
    /// <summary>
    /// The grid the projectile was spawned from.
    /// </summary>
    [ViewVariables]
    public EntityUid? SourceGrid;
}
