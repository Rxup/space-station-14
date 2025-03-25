using Content.Shared.Shuttles.BUIStates;

namespace Content.Server.Shuttles.Components;

[RegisterComponent]
public sealed partial class RadarDetectableComponent : Component
{
    /// <summary>
    /// The name the radar will see when this entity is spotted
    /// </summary>
    [DataField]
    public string? RadarName;

    /// <summary>
    /// Is this entity currently spottable?
    /// </summary>
    [DataField]
    public bool Spottable = true;

    /// <summary>
    /// Depends on the draw type.
    /// Circle - radius
    /// Rectangle - smaller line length, the bigger is smaller * 1.5
    /// Square - line length
    /// And I do not fucking know what is it for entity, just do not put more than 1.. Because it would be abysmally big
    /// </summary>
    [DataField]
    public float DetectableSize = 4f;

    [DataField]
    public Color? DetectableColor;

    /// <summary>
    /// How do you want to draw a detectable?
    /// </summary>
    [DataField]
    public DetectableDrawType DrawType = DetectableDrawType.Circle;

    /// <summary>
    /// Distance from the radar point at which the detectable entity will be seen
    /// </summary>
    [DataField]
    public float SpottingDistance = 64f;
}
