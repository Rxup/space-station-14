using Content.Shared.EntityTable.EntitySelectors;
using Robust.Shared.Map;

namespace Content.Shared.Backmen.AirDrop;

public sealed class AirDropSpawnEvent : HandledEntityEventArgs
{
    public MapCoordinates Pos { get; set; }
}
public sealed class AirDropTargetSpawnEvent : HandledEntityEventArgs
{
    public MapCoordinates Pos { get; set; }
}
public sealed class AirDropItemSpawnEvent : HandledEntityEventArgs
{
    public required EntityTableSelector DropTable { get; set; }
    public EntityUid SupplyPod { get; set; }
    public bool ForceOpenSupplyDrop { get; set; } = false;
}
