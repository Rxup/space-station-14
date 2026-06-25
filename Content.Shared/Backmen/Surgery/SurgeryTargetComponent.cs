using Robust.Shared.GameStates;

namespace Content.Shared.Backmen.Surgery;

[RegisterComponent, NetworkedComponent]
public sealed partial class SurgeryTargetComponent : Component
{
    /// <summary>
    /// Whether this entity can perform surgery on others.
    /// </summary>
    [DataField]
    public bool CanOperate = true;

    /// <summary>
    /// Whether surgery can be performed on this entity.
    /// </summary>
    [DataField]
    public bool CanBeOperatedOn = true;
}
