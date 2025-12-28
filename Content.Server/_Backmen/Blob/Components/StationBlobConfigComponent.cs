using System.ComponentModel.DataAnnotations;
using Content.Server._Backmen.SpecForces;
using Robust.Shared.Prototypes;

namespace Content.Server._Backmen.Blob.Components;

[RegisterComponent]
public sealed partial class StationBlobConfigComponent : Component
{
    public const int DefaultStageBegin = 30;
    public const int DefaultStageCritical = 400;
    public const int DefaultStageEnd = 800;

    [DataField("stageBegin")]
    public int StageBegin { get; set; } = DefaultStageBegin;

    [DataField("stageCritical")]
    public int StageCritical { get; set; } = DefaultStageCritical;

    [DataField("stageTheEnd")]
    public int StageTheEnd { get; set; } = DefaultStageEnd;

    [DataField("specForceTeam")]
    public ProtoId<SpecForceTeamPrototype> SpecForceTeam { get; set; } = "RXBZZBlobDefault";
}
