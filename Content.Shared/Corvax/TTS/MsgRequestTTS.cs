using Lidgren.Network;
using Robust.Shared.Network;
using Robust.Shared.Serialization;

namespace Content.Shared.Corvax.TTS;

public enum VoiceRequestType
{
    None,
    Preview
}

// ReSharper disable once InconsistentNaming
public sealed class MsgRequestTTS : NetMessage
{
    public override MsgGroups MsgGroup => MsgGroups.Command;

    public NetEntity Uid { get; set; } = NetEntity.Invalid;
    public VoiceRequestType Text { get; set; } = VoiceRequestType.None;
    public string VoiceId { get; set; } = String.Empty;

    public override void ReadFromBuffer(NetIncomingMessage buffer, IRobustSerializer serializer)
    {
        Uid = buffer.ReadNetEntity();
        Text = (VoiceRequestType)buffer.ReadInt32();
        VoiceId = buffer.ReadString();
    }

    public override void WriteToBuffer(NetOutgoingMessage buffer, IRobustSerializer serializer)
    {
        buffer.Write(Uid);
        buffer.Write((int)Text);
        buffer.Write(VoiceId);
    }
}
