using Content.Shared.Backmen.TTS;
using Content.Shared.Corvax.TTS;
using Robust.Shared.Player;
using Robust.Shared.Random;


// ReSharper disable once CheckNamespace
namespace Content.Server.Corvax.TTS;

public sealed partial class TTSSystem
{
    [Dependency] private readonly IRobustRandom _robustRandom = default!;

    private readonly List<string> _sampleText = new() // TODO: Локализация?
    {
        "Съешь же ещё этих мягких французских булок, да выпей чаю.",
        "Клоун, прекрати разбрасывать банановые кожурки офицерам под ноги!",
        "Капитан, вы уверены что хотите назначить клоуна на должность главы персонала?",
        "Эс Бэ! Тут человек в сером костюме, с тулбоксом и в маске! Помогите!!"
    };

    /// <summary>
    /// Вообще не понимаю на какой хрен позволять пользователяем ддосить сервер ттса да и еще своим любым текстом -_-
    /// </summary>
    /// <param name="ev"></param>
    private async void OnRequestGlobalTTS(RequestGlobalTTSEvent ev, EntitySessionEventArgs args)
    {
        if (!_isEnabled ||
            !_prototypeManager.TryIndex<TTSVoicePrototype>(ev.VoiceId, out var protoVoice))
            return;

        if (ev.Text != VoiceRequestType.Preview)
        {
            return;
        }

        var txt = _robustRandom.Pick(_sampleText);
        var cacheId = GetCacheId(protoVoice, $"{ev.Text.ToString()}-{_sampleText.IndexOf(txt)}");

        var cached = await GetFromCache(cacheId);
        if (cached != null)
        {
            RaiseNetworkEvent(new PlayTTSEvent(cached), Filter.SinglePlayer(args.SenderSession));
            return;
        }

        var soundData = await GenerateTTS(txt, protoVoice.Speaker);
        if (soundData is null)
            return;

        RaiseNetworkEvent(new PlayTTSEvent(soundData), Filter.SinglePlayer(args.SenderSession));

        await SaveVoiceCache(cacheId, soundData);
    }
}
