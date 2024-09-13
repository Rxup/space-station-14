using Content.Shared.DeviceLinking;
using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Backmen.Teams.CapturePoint.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class BkmCapturePointComponent : Component
{
    [DataField("team"), AutoNetworkedField]
    public StationTeamMarker Team = StationTeamMarker.Neutral;
    [DataField("captureMax")]
    public FixedPoint2 CaptureMax = 60;
    /// <summary>
    /// Значение прибавки при захвате в 1сек
    /// </summary>
    [DataField("captureTick")]
    public FixedPoint2 CaptureTick = 1;

    [AutoNetworkedField]
    public FixedPoint2 CaptureCurrent = FixedPoint2.Zero;


    [DataField("captureZone")]
    public FixedPoint2 ZoneRange = 5;

    public HashSet<EntityUid> CapturedEntities = new();
    public float Acc = 0;

    /// <summary>
    /// Name of the output port.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public ProtoId<SourcePortPrototype> OutputPortTeamA = "OutputTeamA";

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public ProtoId<SourcePortPrototype> OutputPortTeamB = "OutputTeamB";
}
