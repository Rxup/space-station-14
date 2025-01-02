using Content.Shared.Access;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Backmen.AccessWeaponBlockerSystem;

[RegisterComponent,NetworkedComponent,AutoGenerateComponentState]
public sealed partial class AccessWeaponBlockerComponent : Component
{
    [ViewVariables(VVAccess.ReadWrite),AutoNetworkedField]
    public bool CanUse;

    [ViewVariables(VVAccess.ReadWrite),AutoNetworkedField]
    [DataField("alertText")]
    public LocId AlertText = "";

    [ViewVariables(VVAccess.ReadWrite),
     DataField("access")]
    public HashSet<ProtoId<AccessLevelPrototype>> Access = new();
}
