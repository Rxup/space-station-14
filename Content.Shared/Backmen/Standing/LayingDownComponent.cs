using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Backmen.Standing;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class LayingDownComponent : Component
{
    [DataField, AutoNetworkedField, ViewVariables(VVAccess.ReadWrite)]
    public float StandingUpTime { get; set; } = 1f;

    [DataField, AutoNetworkedField, ViewVariables(VVAccess.ReadWrite)]
    public float SpeedModify { get; set; } = 0.4f;

    [DataField, AutoNetworkedField]
    public int? OriginalDrawDepth { get; set; }
}
[Serializable, NetSerializable]
public sealed class ChangeLayingDownEvent : CancellableEntityEventArgs;

/*
[Serializable, NetSerializable]
public sealed class CheckAutoGetUpEvent(NetEntity user) : CancellableEntityEventArgs
{
    public NetEntity User = user;
}
*/
[Serializable, NetSerializable]
public sealed class DrawDownedEvent(NetEntity uid) : EntityEventArgs
{
    public NetEntity Uid = uid;
}
