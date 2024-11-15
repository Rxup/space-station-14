using Content.Shared.Actions;
using Robust.Shared.Serialization;

namespace Content.Shared.Mech;

public sealed partial class MechInhaleEvent : InstantActionEvent
{
}

public sealed partial class MechTurnLightsEvent : InstantActionEvent
{
}

/// <summary>
/// Raised on mech equipment destruction.
/// </summary>
[ByRefEvent]
public record struct MechEquipmentDestroyedEvent();

/// <summary>
/// Raised on the mech during pilot setup
/// </summary>
/// <param name="Pilot"></param>
[ByRefEvent]
public record struct SetupMechUserEvent(EntityUid Pilot);

/// <summary>
/// Sent to server when player selects mech equipment in radial menu.
/// </summary>
[Serializable, NetSerializable]
public sealed partial class SelectMechEquipmentEvent : EntityEventArgs
{
    public NetEntity User;
    public NetEntity? Equipment;

    public SelectMechEquipmentEvent(NetEntity user, NetEntity? equipment)
    {
        User = user;
        Equipment = equipment;
    }
}

[Serializable, NetSerializable]
public sealed partial class PopulateMechEquipmentMenuEvent : EntityEventArgs
{
    public List<NetEntity> Equipment;

    public PopulateMechEquipmentMenuEvent(List<NetEntity> equipment)
    {
        Equipment = equipment;
    }
}

/// <summary>
/// Exsists just to avoid exceptions but close radial menu on mech exit
/// </summary>
[Serializable, NetSerializable]
public sealed partial class CloseMechMenuEvent : EntityEventArgs
{
}
