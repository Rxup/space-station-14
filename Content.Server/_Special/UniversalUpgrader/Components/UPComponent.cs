
namespace Content.Server._Special.UniversalUpgrader.Components;

[RegisterComponent]
public sealed partial class UPComponent : Component
{
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public string upgradeName;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public string componentName;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public string upgradeValue;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public string ProtoWhitelist;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public int usable = 0;
}
