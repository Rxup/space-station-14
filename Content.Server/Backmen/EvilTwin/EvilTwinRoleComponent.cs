using Content.Shared.Mind;
using Content.Shared.Roles;
using Content.Shared.Roles.Components;

namespace Content.Server.Backmen.EvilTwin;

[RegisterComponent]
public sealed partial class EvilTwinRoleComponent : BaseMindRoleComponent
{
    public EntityUid? Target { get; set; }
    public EntityUid? TargetMindId { get; set; }
    public MindComponent? TargetMind { get; set; }
}
