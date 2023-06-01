using Content.Server.SS220.Chat.Systems;
using Content.Shared.Corvax.TTS;
using Content.Shared.SS220.AnnounceTTS;

namespace Content.Server.Corvax.TTS;

public sealed partial class TTSSystem
{
    private string _voiceId = "Announcer";

    private async void OnAnnouncementSpoke(AnnouncementSpokeEvent args)
    {
        if (!_isEnabled ||
            args.Message.Length > MaxMessageChars * 2 ||
            !_prototypeManager.TryIndex<TTSVoicePrototype>(_voiceId, out var protoVoice))
        {
            RaiseNetworkEvent(new AnnounceTTSEvent(new byte[]{}, args.AnnouncementSound, args.AnnouncementSoundParams), args.Source);
            return;
        }

        var soundData = await GenerateTTS(args.Message, protoVoice.Speaker);
        soundData ??= new byte[] { };
        RaiseNetworkEvent(new AnnounceTTSEvent(soundData, args.AnnouncementSound, args.AnnouncementSoundParams), args.Source);
    }
}
