

using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.Server.Backmen.SpecForces;

[RegisterComponent]
public sealed class SpecForceComponent : Component
{
    [DataField("requirements")]
    public HashSet<JobRequirement>? Requirements;

    /// <summary>
    ///     Extra components to add to this entity.
    /// </summary>
    [DataField("components")]
    public EntityPrototype.ComponentRegistry? Components { get; }
}
