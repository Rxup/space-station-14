using Content.Shared.Roles;

namespace Content.Server.Backmen.Vampiric.Role;

[RegisterComponent]
public sealed partial class VampireRoleComponent : AntagonistRoleComponent
{
    public EntityUid? MasterVampire;
}

