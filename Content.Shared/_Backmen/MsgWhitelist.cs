using Lidgren.Network;
using Robust.Shared.Network;
using Robust.Shared.Serialization;

namespace Content.Shared.Backmen;

public sealed class MsgWhitelist : NetMessage
{
    public override MsgGroups MsgGroup => MsgGroups.EntityEvent;

    public bool Whitelisted = false;

    public override void ReadFromBuffer(NetIncomingMessage buffer, IRobustSerializer serializer)
    {
        Whitelisted = buffer.ReadBoolean();
    }

    public override void WriteToBuffer(NetOutgoingMessage buffer, IRobustSerializer serializer)
    {
        buffer.Write(Whitelisted);
    }
}
