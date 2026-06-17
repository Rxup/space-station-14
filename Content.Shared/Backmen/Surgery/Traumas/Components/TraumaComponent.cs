using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;

namespace Content.Shared.Backmen.Surgery.Traumas.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(fieldDeltas: true, raiseAfterAutoHandleState: true)]
public sealed partial class TraumaComponent : Component
{
    /// <summary>
    /// Self-explanatory. Can be null if the organ or bone, etc; got delimbed but still exists
    /// </summary>
    [AutoNetworkedField, ViewVariables(VVAccess.ReadOnly)]
    public EntityUid? HoldingWoundable;

    /// <summary>
    /// Self-explanatory
    /// For OrganDamage - the organ
    /// For BoneDamage - the bone
    /// For VeinsDamage and NerveDamage - the woundable
    /// For Dismemberment - the parent woundable, of the woundable that got delimbed
    /// </summary>
    [AutoNetworkedField, ViewVariables(VVAccess.ReadOnly)]
    public EntityUid? TraumaTarget;

    /// <summary>
    /// The severity the wound had when trauma got induced; Gets updated to the new one if the trauma gets worsened by the same wound
    /// </summary>
    [AutoNetworkedField, ViewVariables(VVAccess.ReadOnly)]
    public FixedPoint2 TraumaSeverity;

    /// <summary>
    /// Self-explanatory
    /// </summary>
    [AutoNetworkedField, DataField, ViewVariables(VVAccess.ReadOnly)]
    public TraumaType TraumaType;
}
