

using Content.Shared.Roles;

namespace Content.Server.Backmen.SpecForces;

[RegisterComponent]
public sealed class SpecForceComponent : Component
{
    [DataField("requirements")]
    public HashSet<JobRequirement>? Requirements;
}
