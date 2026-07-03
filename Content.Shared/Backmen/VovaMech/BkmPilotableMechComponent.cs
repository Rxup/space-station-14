using Robust.Shared.Containers;
using Robust.Shared.GameStates;

namespace Content.Shared.Backmen.VovaMech;

/// <summary>
/// Marker for OneStar silicon mechs that support player piloting via ContainerVehicle.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class BkmPilotableMechComponent : Component
{
    [DataField]
    public float EntryDelay = 3f;

    [ViewVariables]
    public readonly string PilotSlotId = "mech-pilot-slot";

    [ViewVariables]
    public ContainerSlot PilotSlot = default!;

    /// <summary>
    /// Whether <see cref="GhostTakeoverAvailableComponent"/> was present before a pilot entered.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public bool HadGhostTakeover;
}
