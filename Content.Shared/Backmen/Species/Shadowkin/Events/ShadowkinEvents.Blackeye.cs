using Robust.Shared.Serialization;

namespace Content.Shared.Backmen.Species.Shadowkin.Events;

/// <summary>
///     Raised to notify other systems of an attempt to blackeye a shadowkin.
/// </summary>
public sealed class ShadowkinBlackeyeAttemptEvent : CancellableEntityEventArgs
{
    public readonly NetEntity Uid;

    public ShadowkinBlackeyeAttemptEvent(NetEntity uid)
    {
        Uid = uid;
    }
}

/// <summary>
///     Raised when a shadowkin becomes a blackeye.
/// </summary>
[Serializable, NetSerializable]
public sealed class ShadowkinBlackeyeEvent : EntityEventArgs
{
    public readonly NetEntity Uid;
    public readonly bool Damage;

    public ShadowkinBlackeyeEvent(NetEntity uid, bool damage = true)
    {
        Uid = uid;
        Damage = damage;
    }
}
