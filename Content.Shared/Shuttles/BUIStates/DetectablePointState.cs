using Content.Shared.Shuttles.Components;
using Robust.Shared.Map;
using Robust.Shared.Serialization;

// backmen edit
namespace Content.Shared.Shuttles.BUIStates;

/// <summary>
/// State of each individual detected point on the scanner map
/// </summary>
[Serializable, NetSerializable]
public sealed class DetectablePointState
{
    public string? Name;

    public DetectableDrawType DrawType;
    public float DetectableSize;

    public Color? Color;

    public NetCoordinates Coordinates;
    public Angle? Angle;
    public NetEntity Entity;
}

[Serializable, NetSerializable]
public enum DetectableDrawType : byte
{
    Rectangle,
    Circle,
    Square,
    Entity,
}

/// <summary>
/// Raised on an entity when trying to spot it on a radar.
/// </summary>
[ByRefEvent]
public readonly record struct SpottingAttemptEvent(Entity<RadarConsoleComponent> Spotter, bool Cancelled = false);
