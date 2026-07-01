using Content.Shared._Mono.Radar;
using Robust.Shared.GameStates;

namespace Content.Shared._Mono.Radar;

[RegisterComponent, NetworkedComponent]
public sealed partial class RadarBlipComponent : Component
{
    [ViewVariables(VVAccess.ReadWrite), DataField("radarColor")]
    public Color RadarColor = Color.Red;

    [ViewVariables(VVAccess.ReadWrite), DataField("highlightedRadarColor")]
    public Color HighlightedRadarColor = Color.OrangeRed;

    [DataField]
    public float Scale = 1;

    [DataField]
    public RadarBlipShape Shape = RadarBlipShape.Circle;

    [DataField]
    public bool RequireNoGrid = false;

    [DataField]
    public bool VisibleFromOtherGrids = true;

    [DataField]
    public bool Enabled = true;

    [DataField]
    public float MaxDistance = 1024f;
}
