using Content.Shared.Body;
using Content.Shared.Body.Part;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Medical.Surgery.Steps;

/// <summary>
/// Attaches a graft organ by exact category, or into the first open spider-leg slot on a side.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class SurgeryOrganGraftAttachComponent : Component
{
    /// <summary>
    /// Exact organ category the held graft must match.
    /// </summary>
    [DataField]
    public ProtoId<OrganCategoryPrototype>? RequiredCategory;

    /// <summary>
    /// When set, accepts any spider-leg graft on that side and assigns the first missing slot.
    /// </summary>
    [DataField]
    public BodyPartSymmetry? SpiderLegSide;

    /// <summary>
    /// Organ category that must already be present on the patient before this graft can be performed.
    /// </summary>
    [DataField]
    public ProtoId<OrganCategoryPrototype>? PrerequisiteCategory;
}
