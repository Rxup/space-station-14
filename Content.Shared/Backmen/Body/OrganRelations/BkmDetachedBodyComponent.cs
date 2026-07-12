using Robust.Shared.GameStates;

namespace Content.Shared.Backmen.Body.OrganRelations;

/// <summary>
/// Marker for the temporary body container spawned when external organs are surgically detached.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(raiseAfterAutoHandleState: true)]
public sealed partial class BkmDetachedBodyComponent : Component
{
    /// <summary>
    /// Wide scatter placement (gib / violent detach) vs neat surgical drop.
    /// </summary>
    [AutoNetworkedField]
    public bool MessyScatter;

    /// <summary>
    /// The primary organ that was detached (e.g. the arm, not the hand).
    /// </summary>
    [AutoNetworkedField]
    public EntityUid? RootOrgan;
}
