using Robust.Shared.Serialization;

namespace Content.Shared.Backmen.Surgery;

[Serializable, NetSerializable]
public sealed class SurgeryUiRefreshEvent : EntityEventArgs
{
    public NetEntity Uid { get; }

    public SurgeryUiRefreshEvent(NetEntity uid)
    {
        Uid = uid;
    }
}
