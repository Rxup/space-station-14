using Content.Shared.SD;
using Robust.Shared.GameStates;

namespace Content.Shared.DetailExaminable;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class DetailExaminableComponent : Component
{
    [DataField(required: true), AutoNetworkedField]
    public string Content = string.Empty;

    // SD-ERPStatus-start
    [DataField("ERPStatus", required: true)]
    [ViewVariables(VVAccess.ReadWrite)]
    public EnumERPStatus ERPStatus = EnumERPStatus.NO;

    public string GetERPStatusName()
    {
        switch (ERPStatus)
        {
            case EnumERPStatus.HALF:
                return Loc.GetString("humanoid-erp-status-half");
            case EnumERPStatus.FULL:
                return Loc.GetString("humanoid-erp-status-full");
            default:
                return Loc.GetString("humanoid-erp-status-no");
        }
    }
    // SD-ERPStatus-end
}
