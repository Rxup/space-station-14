

using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;

namespace Content.Shared.Backmen.Surgery.Traumas.Components;

/// <summary>
/// This is used for...
/// </summary>
[RegisterComponent, AutoGenerateComponentState, NetworkedComponent]
public sealed partial class TraumaComponent : Component
{
    /// <summary>
    /// Self-explanatory. Can be null if the organ or bone, etc; got delimbed but still exists
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly)]
    public EntityUid? HoldingWoundable;

    /// <summary>
    /// Self-explanatory
    /// For OrganDamage - the organ
    /// For BoneDamage - the bone
    /// For VeinsDamage and NerveDamage - the woundable
    /// For Dismemberment - the parent woundable, of the woundable that got delimbed
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly)]
    public EntityUid? TraumaTarget;

    /// <summary>
    /// The severity the wound had when trauma got induced; Gets updated to the new one if the trauma gets worsened by the same wound
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly), AutoNetworkedField]
    public FixedPoint2 TraumaSeverity;

    /// <summary>
    /// Self-explanatory
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadOnly), AutoNetworkedField]
    public TraumaType TraumaType;
}
