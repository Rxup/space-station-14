using Robust.Shared.GameStates;

namespace Content.Shared.Backmen.Body.OrganRelations;

/// <summary>
/// Detached brain organs are immune to further damage, fire, and rotting.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class BkmDetachedBrainProtectionComponent : Component;
