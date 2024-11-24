using Content.Shared.Access;
using Content.Shared.Backmen.AccessWeaponBlockerSystem;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype.Set;

namespace Content.Server.Backmen.AccessWeaponBlockerSystem;

[RegisterComponent]
public sealed partial class AccessWeaponBlockerComponent : SharedAccessWeaponBlockerComponent
{
    [ViewVariables(VVAccess.ReadWrite)]
    public bool CanUse;

    [ViewVariables(VVAccess.ReadWrite)]
    [DataField("alertText")]
    public string AlertText = "";

    [ViewVariables(VVAccess.ReadWrite),
     DataField("access", customTypeSerializer: typeof(PrototypeIdHashSetSerializer<AccessLevelPrototype>))]
    public HashSet<string> Access = new();
}
