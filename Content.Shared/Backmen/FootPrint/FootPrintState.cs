using Robust.Shared.Serialization;

namespace Content.Shared.Backmen.FootPrint;

[Serializable, NetSerializable]
public sealed class FootPrintState(NetEntity netEntity) : ComponentState
{
    public NetEntity PrintOwner { get; private set; } = netEntity;
}
