using Content.Shared.Mind;
using Content.Shared.Roles;

namespace Content.Server.Backmen.EvilTwin;

[RegisterComponent]
public sealed partial class EvilTwinRoleComponent : BaseMindRoleComponent
{
    public EntityUid? Target { get; set; }
    public EntityUid? TargetMindId { get; set; }
    public MindComponent? TargetMind { get; set; }
}
