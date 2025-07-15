using Robust.Shared.Prototypes;

namespace Content.Shared.Backmen.AirDrop;

[RegisterComponent]
public sealed partial class AirDropGhostRoleComponent : Component
{
    [DataField(required: true)]
    public EntProtoId AfterTakePod { get; set; }
    public EntProtoId<AirDropComponent>? SupplyDrop { get; set; }
}
