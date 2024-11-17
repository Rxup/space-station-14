using Content.Shared.Backmen.AccessGunBlockerSystem;

namespace Content.Client.Backmen.AccessWeaponBlockerSystem;

[RegisterComponent]
public sealed partial class AccessWeaponBlockerComponent : SharedAccessWeaponBlockerComponent
{
    [ViewVariables(VVAccess.ReadWrite)]
    public bool CanUse;

    [ViewVariables(VVAccess.ReadWrite)]
    public string AlertText = "";
}
