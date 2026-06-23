using Content.Shared.Body;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Medical.Surgery.Conditions;

/// <summary>
/// Requires that a nubody organ category is present or absent on the patient.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class SurgeryOrganCategoryMissingConditionComponent : Component
{
    [DataField(required: true)]
    public ProtoId<OrganCategoryPrototype> Category;

    /// <summary>
    /// When true, the category must be absent (for graft attach surgeries).
    /// </summary>
    [DataField]
    public bool Inverse;
}
