using Content.Shared.Roles.Components;

namespace Content.Server.Backmen.Vampiric.Role;

[RegisterComponent]
public sealed partial class VampireRoleComponent : BaseMindRoleComponent
{
    public EntityUid? MasterVampire;

    [ViewVariables(VVAccess.ReadWrite)]
    public int Tier = 0;

    [ViewVariables(VVAccess.ReadWrite)]
    public float Drink = 0;

    [ViewVariables(VVAccess.ReadWrite)]
    public int Converted = 0;
}

