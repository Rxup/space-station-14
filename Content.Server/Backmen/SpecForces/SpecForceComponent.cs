using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.Server.Backmen.SpecForces;

[RegisterComponent]
public sealed partial class SpecForceComponent : Component
{
    [ViewVariables(VVAccess.ReadWrite)]
    [DataField("actionName")]
    public string? ActionName { get; private set; }

    [DataField("requirements")]
    public HashSet<JobRequirement>? Requirements { get; private set; }

    /// <summary>
    /// A dictionary mapping the component type list to the YAML mapping containing their settings.
    /// </summary>

    [DataField("components")]
    [AlwaysPushInheritance]
    public ComponentRegistry Components { get; private set; } = new();

    [DataField("whitelistRequired")]
    public bool WhitelistRequired { get; set; } = false;
}
