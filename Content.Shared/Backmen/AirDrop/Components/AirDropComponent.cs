using Content.Shared.EntityTable;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Backmen.AirDrop;

[RegisterComponent, NetworkedComponent]
public sealed partial class AirDropItemComponent : Component
{
    [DataField(required: true)]
    public EntProtoId<AirDropComponent> AirDropProto { get; private set; }

    [DataField]
    public TimeSpan Cooldown { get; private set; } = TimeSpan.FromMinutes(5);

    [DataField]
    public bool StartCooldown { get; private set; } = false;
}

[RegisterComponent, NetworkedComponent]
public sealed partial class AirDropComponent : Component
{
    [DataField]
    public EntProtoId DropTargetProto { get; private set; } = "DropPodMarkerSimple";

    [DataField]
    [AlwaysPushInheritance]
    public ComponentRegistry DropTarget { get; private set; } = new();

    /// <summary>
    /// Время цели до анимации падения
    /// в секундах
    /// </summary>
    [DataField]
    public float TimeOfTarget { get; set; } = 2;

    /// <summary>
    /// Время анимации панедения до спавна пода
    /// в секундах
    /// </summary>
    [DataField]
    public float TimeToDrop { get; set; } = 2;

    [DataField]
    public EntProtoId InAirProto { get; private set; } = "DropPodLaunchAnimationSimple";

    [DataField]
    [AlwaysPushInheritance]
    public ComponentRegistry InAir { get; private set; } = new();

    [DataField]
    public EntProtoId SupplyDropProto { get; private set; } = "SupplyDropPodEmpty";

    [DataField]
    [AlwaysPushInheritance]
    public ComponentRegistry SupplyDrop { get; private set; } = new();

    [AlwaysPushInheritance]
    [DataField]
    public ProtoId<EntityTablePrototype>? SupplyDropTable { get; private set; }

    [DataField]
    public bool ForceOpenSupplyDrop = false;
}
