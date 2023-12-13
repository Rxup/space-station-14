using Robust.Shared.Prototypes;

namespace Content.Server.Backmen.SpecForces;

[RegisterComponent]
public sealed partial class SpecForceComponent : Component
{
    [ViewVariables(VVAccess.ReadWrite)]
    [DataField("actionBssActionName")]
    public string? ActionBssActionName { get; private set; }

    /// <summary>
    /// A dictionary mapping the component type list to the YAML mapping containing their settings.
    /// </summary>

    [DataField("components")]
    [AlwaysPushInheritance]
    public ComponentRegistry Components { get; private set; } = new();

    public EntityUid? BssKey = null;
}
