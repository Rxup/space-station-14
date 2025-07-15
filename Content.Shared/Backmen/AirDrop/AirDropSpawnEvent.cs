using Content.Shared.EntityTable.EntitySelectors;
using Robust.Shared.Map;
using Robust.Shared.Serialization;

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

[Serializable,NetSerializable]
public sealed class AirDropStartEvent : EntityEventArgs
{
    public NetEntity Uid { get; set; }
    public MapCoordinates Pos { get; set; }
}
