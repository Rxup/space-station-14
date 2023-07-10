

using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.Server.Backmen.SpecForces;

[RegisterComponent]
public sealed class SpecForceComponent : Component
{
    [ViewVariables(VVAccess.ReadWrite)]
    [DataField("actionName")]
    public string? ActionName;

    [DataField("requirements")]
    public HashSet<JobRequirement>? Requirements;

    /// <summary>
    /// A dictionary mapping the component type list to the YAML mapping containing their settings.
    /// </summary>

    [DataField("components")]
    [AlwaysPushInheritance]
    public ComponentRegistry Components { get; } = new();
}
