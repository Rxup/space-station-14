using Robust.Shared.Prototypes;
using Content.Shared.Random;
using Content.Shared.Roles;
using Content.Shared.Silicons.Laws;

namespace Content.Shared.Backmen.StationAI;

[RegisterComponent]
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
    public SiliconLawset? SelectedLaw;

    [ViewVariables(VVAccess.ReadWrite)]
    public string? SelectedLawId;

    [ViewVariables(VVAccess.ReadOnly)]
    public bool Broken = false;

    /// <summary>
    /// A role given to entities with this component when they are emagged.
    /// Mostly just for admin purposes.
    /// </summary>
    [DataField]
    public ProtoId<AntagPrototype>? AntagonistRole = "SubvertedSilicon";
}
