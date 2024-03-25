using Robust.Shared.Serialization;

namespace Content.Shared.Backmen.Ghost.Roles.Events;

[Serializable, NetSerializable]
public sealed class CancelGhostRollerEvent : EntityEventArgs
{
    public uint Id { get; set; }
}
