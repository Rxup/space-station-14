using Robust.Shared.GameStates;

namespace Content.Shared.Backmen.Surgery.Conditions;

/// <summary>
/// Filters surgeries by whether the selected target is an arachne graft organ (cephalothorax, abdomen, spider legs).
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class SurgeryArachneGraftOrganConditionComponent : Component
{
    /// <summary>
    /// When true, the surgery is cancelled if the target is an arachne graft organ.
    /// </summary>
    [DataField]
    public bool Inverse;
}
