using Robust.Shared.Prototypes;
using Content.Shared.Random;
using Content.Shared.Silicons.Laws;
using Robust.Shared.GameStates;

namespace Content.Shared.Backmen.StationAI;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class StationAIComponent : Component
{
    [DataField("action")]
    public EntProtoId Action = "AIHealthOverlay";

    public EntityUid? ActionId;

    [ViewVariables(VVAccess.ReadOnly)]
    public EntityUid ActiveEye = EntityUid.Invalid;

    [DataField("lawsId")]
    public ProtoId<WeightedRandomPrototype> LawsId = "LawsStationAIDefault";

    [ViewVariables(VVAccess.ReadWrite)]
    public SiliconLawsetPrototype? SelectedLaw;

    [DataField("nukeToggle")]
    public EntProtoId NukeToggle = "AIToggleArmNuke";

    public EntityUid? NukeToggleId;

    [DataField("layers", required: true)]
    [ViewVariables]
    public Dictionary<string, PrototypeLayerData[]> Layers = new();

    [DataField("defaultLayer", required: true)]
    [AutoNetworkedField]
    [ViewVariables(VVAccess.ReadWrite)]
    public string SelectedLayer = "blue";
}
