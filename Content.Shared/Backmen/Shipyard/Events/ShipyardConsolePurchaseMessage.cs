using Content.Shared.Backmen.Shipyard.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Backmen.Shipyard.Events;

/// <summary>
///     Purchase a Vessel from the console
/// </summary>
[Serializable, NetSerializable]
public sealed class ShipyardConsolePurchaseMessage(ProtoId<VesselPrototype> vessel) : BoundUserInterfaceMessage
{
    public ProtoId<VesselPrototype> Vessel = vessel; //vessel prototype ID
}
