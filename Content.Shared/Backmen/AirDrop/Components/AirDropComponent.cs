using Content.Shared.EntityTable;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

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

    [DataField]
    public bool DeleteOnUse { get; private set; } = true;

    [DataField]
    public bool LavaLandOnly { get; private set; } = false;
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class AirDropComponent : Component
{
    [DataField, AutoNetworkedField]
    public AirDropPhase Phase = AirDropPhase.Inactive;

    /// <summary>
    /// When the current <see cref="Phase"/> ends.
    /// </summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField, AutoNetworkedField]
    public TimeSpan PhaseEndTime = TimeSpan.Zero;

    /// <summary>
    /// Client-side holographic target marker (with <c>AnimationPlayer</c>).
    /// </summary>
    [ViewVariables]
    public EntityUid? TargetMarker;

    /// <summary>
    /// Client-side falling pod animation (with <c>AnimationPlayer</c>).
    /// </summary>
    [ViewVariables]
    public EntityUid? InAirMarker;

    /// <summary>
    /// Last <see cref="Phase"/> processed by client visuals.
    /// </summary>
    [ViewVariables]
    public AirDropPhase ClientLastPhase = AirDropPhase.Inactive;

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
    public float TimeOfTarget { get; set; } = 1;

    /// <summary>
    /// Время анимации панедения до спавна пода
    /// в секундах
    /// </summary>
    [DataField]
    public float TimeToDrop { get; set; } = 2;

    /// <summary>
    /// Дальность (в метрах) рассылки <see cref="AirDropStartEvent"/> сверх стандартного PVS.
    /// Игроки, подбежавшие позже, всё равно получат визуал через синхронизированную <see cref="Phase"/> в Update.
    /// </summary>
    [DataField]
    public float VisualNotifyRange = 60f;

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
