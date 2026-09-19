using Robust.Shared.Serialization;

namespace Content.Shared.Backmen.Targeting;

[Serializable, NetSerializable]
public sealed class TargetChangeEvent : EntityEventArgs
{
    public NetEntity Uid { get; }
    public TargetBodyPart BodyPart { get; }
    public TargetChangeEvent(NetEntity uid, TargetBodyPart bodyPart)
    {
        Uid = uid;
        BodyPart = bodyPart;
    }
}

[Serializable, NetSerializable]
public sealed class TargetIntegrityChangeEvent : EntityEventArgs
{
    public NetEntity Uid { get; }
    public bool RefreshUi { get; }
    public TargetIntegrityChangeEvent(NetEntity uid, bool refreshUi = true)
    {
        Uid = uid;
        RefreshUi = refreshUi;
    }
}

/// <summary>
/// Raised on a body when an inventory clothing slot is enabled/disabled due to amputation or reattachment.
/// Client removes or re-adds the corresponding hotbar button.
/// </summary>
public sealed class RefreshInventorySlotsEvent : EntityEventArgs
{
    public string SlotName { get; }

    public RefreshInventorySlotsEvent(string slotName)
    {
        SlotName = slotName;
    }
}
