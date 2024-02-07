﻿using Content.Shared.Corvax.CCCVars;
using Content.Shared.Corvax.TTS;
using Content.Shared.GameTicking;
using Robust.Client.ResourceManagement;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.ContentPack;
using Robust.Shared.Player;
using Robust.Shared.Utility;

namespace Content.Client.Corvax.TTS;

/// <summary>
/// Plays TTS audio in world
/// </summary>
// ReSharper disable once InconsistentNaming
public sealed class TTSSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IResourceCache _resourceCache = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    //[Dependency] private readonly SharedGameTicker _gameTicker = default!;

    private ISawmill _sawmill = default!;
    private readonly MemoryContentRoot _contentRoot = new();
    private ResPath _prefix;

    /// <summary>
    /// Reducing the volume of the TTS when whispering. Will be converted to logarithm.
    /// </summary>
    private const float WhisperFade = 4f;

    /// <summary>
    /// The volume at which the TTS sound will not be heard.
    /// </summary>
    private const float MinimalVolume = -10f;

    private float _volume = 0.0f;
    private ulong _fileIdx = 0;
    private static ulong _shareIdx = 0;

    public override void Initialize()
    {
        _prefix = ResPath.Root / $"TTS{_shareIdx++}";
        _sawmill = Logger.GetSawmill("tts");
        _resourceCache.AddRoot(_prefix, _contentRoot);
        _cfg.OnValueChanged(CCCVars.TTSVolume, OnTtsVolumeChanged, true);
        SubscribeNetworkEvent<PlayTTSEvent>(OnPlayTTS);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
    }

    private void OnRoundRestart(RoundRestartCleanupEvent ev)
    {
        _contentRoot.Clear();
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _cfg.UnsubValueChanged(CCCVars.TTSVolume, OnTtsVolumeChanged);
        _contentRoot.Dispose();
    }

    public void RequestGlobalTTS(Content.Shared.Backmen.TTS.VoiceRequestType text, string voiceId)
    {
        RaiseNetworkEvent(new RequestGlobalTTSEvent(text, voiceId));
    }

    private void OnTtsVolumeChanged(float volume)
    {
        _volume = volume;
    }

    private void OnPlayTTS(PlayTTSEvent ev)
    {
        //_sawmill.Debug($"Play TTS audio {ev.Data.Length} bytes from {ev.SourceUid} entity");

        var volume = AdjustVolume(ev.IsWhisper);

        var filePath = new ResPath($"{_fileIdx++}.ogg");
        _contentRoot.AddOrUpdateFile(filePath, ev.Data);

        var audioParams = AudioParams.Default.WithVolume(volume);
        var soundPath = new SoundPathSpecifier(_prefix / filePath, audioParams);
        if (ev.SourceUid != null)
        {
            var sourceUid = GetEntity(ev.SourceUid.Value);
            if(sourceUid.Valid)
                _audio.PlayEntity(soundPath, Filter.Local(), sourceUid, false, audioParams); // recipient arg ignored on client
        }
        else
        {
            _audio.PlayGlobal(soundPath, Filter.Local(), false);// recordReplay arg ignored on client
        }

        _contentRoot.RemoveFile(filePath);
    }

    private float AdjustVolume(bool isWhisper)
    {
        var volume = Math.Max(MinimalVolume, SharedAudioSystem.GainToVolume(_volume));

        if (isWhisper)
        {
            volume -= SharedAudioSystem.GainToVolume(WhisperFade);
        }

        return volume;
    }
}
