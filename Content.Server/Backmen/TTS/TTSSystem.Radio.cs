


using System.Threading.Tasks;
using Content.Server.Backmen.TTS;
using Content.Server.Radio;
using Content.Shared.Chat;
using Content.Shared.Corvax.TTS;
using Content.Shared.Radio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;
// ReSharper disable once CheckNamespace
using Robust.Shared.Audio;

namespace Content.Server.Corvax.TTS;

public sealed partial class TTSSystem
{
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private EntityQuery<TTSComponent> _ttsQuery = default!;

    private static readonly SoundSpecifier DefaultOnSound =
        new SoundPathSpecifier("/Audio/Backmen/Radio/common.ogg", AudioParams.Default.WithVolume(-6).WithMaxDistance(2));

    public void InitializeRadio()
    {
        SubscribeLocalEvent<TTSComponent, RequestTtsRadioEvent>(OnRequestRadio);
    }

    private async void OnRequestRadio(EntityUid uid, TTSComponent comp, RequestTtsRadioEvent args)
    {
        if (!_isEnabled ||
            uid == args.MessageSource ||
            !_ttsQuery.TryGetComponent(args.MessageSource, out var sourceTts) ||
            sourceTts.VoicePrototypeId is not {} sourceVoice
            )
        {
            _audio.PlayPvs(args.Channel.OnSendSound ?? DefaultOnSound, uid);
            return;
        }

        byte[]? soundData = null;
        try
        {
            soundData = await GenerateTtsRadio(args.Msg.Message.Message, sourceVoice);
        }
        catch (Exception)
        {
            //skip
        }

        if (soundData == null)
        {
            _audio.PlayPvs(args.Channel.OnSendSound ?? DefaultOnSound, uid);
            return;
        }

        RaiseNetworkEvent(new PlayTTSEvent(soundData, null, isHeadset: true), Filter.SinglePlayer(args.Target), false);
    }

    private async Task<byte[]?> GenerateTtsRadio(string text, string speaker)
    {
        var textSanitized = Sanitize(text);
        if (textSanitized == "") return null;
        if (char.IsLetter(textSanitized[^1]))
            textSanitized += ".";

        var textSsml = ToSsmlText(textSanitized, SoundTraits.RateFast | SoundTraits.PitchVerylow);

        // Создаем уникальный ключ на основе всех аргументов
        var taskKey = $"{textSanitized}_{speaker}_radio";

        // Блокируем доступ к словарю, чтобы избежать гонки
        await _lock.WaitAsync();
        try
        {
            // Если задача уже выполняется для этого набора аргументов, ждем её завершения
            if (_ttsTasks.TryGetValue(taskKey, out var existingTask))
            {
                return await existingTask;
            }

            // Создаем задачу и сохраняем её в словарь
            var newTask = _ttsManager.RadioConvertTextToSpeech(speaker, textSsml);
            _ttsTasks[taskKey] = newTask;
        }
        finally
        {
            _lock.Release();
        }

        try
        {
            // Ожидаем завершения задачи
            return await _ttsTasks[taskKey];
        }
        finally
        {
            // Удаляем задачу из словаря независимо от результата
            await _lock.WaitAsync();
            try
            {
                _ttsTasks.Remove(taskKey);
            }
            finally
            {
                _lock.Release();
            }
        }
    }
}

public sealed class RequestTtsRadioEvent
{
    public ICommonSession Target { get; }
    public RadioChannelPrototype Channel { get; }
    public EntityUid MessageSource { get; }
    public MsgChatMessage Msg { get; }

    public RequestTtsRadioEvent(ICommonSession target, RadioChannelPrototype channel, EntityUid messageSource, MsgChatMessage msg)
    {
        Target = target;
        Channel = channel;
        MessageSource = messageSource;
        Msg = msg;
    }
}
