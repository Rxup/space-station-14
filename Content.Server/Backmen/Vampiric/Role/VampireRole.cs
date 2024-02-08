using Content.Shared.Roles;
using Robust.Shared.GameStates;

namespace Content.Server.Backmen.Vampiric.Role;

[RegisterComponent]
[NetworkedComponent]
[AutoGenerateComponentState(false)]
public sealed partial class VampireRoleComponent : AntagonistRoleComponent
{
    public EntityUid? MasterVampire;

    [AutoNetworkedField, ViewVariables(VVAccess.ReadWrite)]
    public float Drink = 0;

    [AutoNetworkedField, ViewVariables(VVAccess.ReadWrite)]
    public int Converted = 0;
}

