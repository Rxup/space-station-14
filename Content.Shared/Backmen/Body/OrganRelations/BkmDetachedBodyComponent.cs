using Robust.Shared.GameStates;

namespace Content.Shared.Backmen.Body.OrganRelations;

/// <summary>
/// Marker for the temporary body container spawned when external organs are surgically detached.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class BkmDetachedBodyComponent : Component
{
    /// <summary>
    /// The primary organ that was detached (e.g. the arm, not the hand).
    /// </summary>
    [AutoNetworkedField]
    public EntityUid? RootOrgan;
}
