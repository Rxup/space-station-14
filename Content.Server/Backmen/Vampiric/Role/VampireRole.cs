using Content.Shared.Roles;
using Robust.Shared.GameStates;

namespace Content.Server.Backmen.Vampiric.Role;

[RegisterComponent]
public sealed partial class VampireRoleComponent : AntagonistRoleComponent
{
    public EntityUid? MasterVampire;

    [ViewVariables(VVAccess.ReadWrite)]
    public int Tier = 0;

    [ViewVariables(VVAccess.ReadWrite)]
    public float Drink = 0;

    [ViewVariables(VVAccess.ReadWrite)]
    public int Converted = 0;
}

