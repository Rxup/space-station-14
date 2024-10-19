using Content.Shared.Roles;

namespace Content.Server.Backmen.Flesh;

[RegisterComponent]
public sealed partial class FleshCultistRoleComponent : BaseMindRoleComponent
{
    [DataField]
    public bool IsLeader = false;
}
