using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Backmen.AirDrop;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class AirDropVisualizerComponent : Component
{
    [ViewVariables(VVAccess.ReadWrite), DataField, AutoNetworkedField]
    public EntProtoId<AirDropComponent> SupplyDrop { get; set; }
    [DataField, AutoNetworkedField]
    public EntProtoId? SupplyDropOverride { get; set; }
}
