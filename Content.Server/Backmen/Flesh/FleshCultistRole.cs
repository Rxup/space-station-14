using Content.Shared.Roles;
using Content.Shared.Roles.Components;

namespace Content.Server.Backmen.Flesh;

[RegisterComponent]
public sealed partial class FleshCultistRoleComponent : BaseMindRoleComponent
{
    [DataField]
    public bool IsLeader = false;
}
