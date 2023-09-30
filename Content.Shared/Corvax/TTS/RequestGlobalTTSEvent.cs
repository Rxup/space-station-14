using Content.Shared.Backmen.TTS;
using Robust.Shared.Serialization;

namespace Content.Shared.Corvax.TTS;

// ReSharper disable once InconsistentNaming
[Serializable, NetSerializable]
public sealed class RequestGlobalTTSEvent : EntityEventArgs
{
    public VoiceRequestType Text { get;}
    public string VoiceId { get; }

    public RequestGlobalTTSEvent(VoiceRequestType text, string voiceId)
    {
        Text = text;
        VoiceId = voiceId;
    }
}
