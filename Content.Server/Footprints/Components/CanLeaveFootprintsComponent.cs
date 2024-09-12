using Robust.Shared.Map;

namespace Content.Server.Footprints.Components;

[RegisterComponent]
public sealed partial class CanLeaveFootprintsComponent : Component
{
    /// <summary>
    /// Where the last footprint was.
    /// </summary>
    [ViewVariables]
    public MapCoordinates LastFootstep;

    /// <summary>
    /// How many footprints left to leave behind the entity.
    /// </summary>
    [ViewVariables]
    public uint FootstepsLeft = 1;

    /// <summary>
    /// If non null represets if the decal is either the alt or normal decal.
    /// Null represents always use normal.
    /// </summary>
    [ViewVariables]
    public bool? UseAlternative;

    [ViewVariables]
    public Color Color = Color.White;
}