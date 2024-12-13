using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Shared.Backmen.Arrivals;

[ByRefEvent]
public struct FlatPackUserAttemptUseEvent(EntityUid user, EntProtoId? itemToSpawn, EntityCoordinates coords)
{
    public readonly EntityUid User = user;
    public readonly EntProtoId? ItemToSpawn = itemToSpawn;
    public readonly EntityCoordinates Coords = coords;
    public bool Cancelled = false;
}
