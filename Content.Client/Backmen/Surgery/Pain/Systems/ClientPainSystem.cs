using System.Linq;
using Content.Shared.Backmen.CCVar;
using Content.Shared.Backmen.Surgery.Pain;
using Content.Shared.Backmen.Surgery.Pain.Components;
using Content.Shared.Backmen.Surgery.Pain.Systems;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;

namespace Content.Client.Backmen.Surgery.Pain.Systems;

public sealed class ClientPainSystem : PainSystem
{
    /// <summary>
    /// The volume at which the pain rattles won't be heard
    /// </summary>
    private const float MinimalVolume = -20f;

    private float _volume;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<PlayPainSoundEvent>(OnPlayPainSound);
        SubscribeNetworkEvent<PlayLoggedPainSoundEvent>(OnPlayLoggedPainSound);

        SubscribeNetworkEvent<KillAllPainSoundsEvent>(OnKillAllSounds);

        Subs.CVar(Cfg, CCVars.BrutalDeathRattlesVolume, value => _volume = value, true);
    }

    private void OnPlayPainSound(PlayPainSoundEvent ev)
    {
        TryGetEntity(ev.Source, out var ent);
        PlayPainSound(ev.Audio, ent, ev.AudioParams);
    }

    private void OnPlayLoggedPainSound(PlayLoggedPainSoundEvent ev)
    {
        TryGetEntity(ev.Source, out var ent);
        TryGetEntity(ev.NerveSystem, out var ns);

        if (!ent.HasValue || !ns.HasValue)
            return;

        PlayPainSound(ent.Value, ns.Value, ev.Audio, ev.AudioParams);
    }

    private void OnKillAllSounds(KillAllPainSoundsEvent ev)
    {
        TryGetEntity(ev.NerveSystem, out var nerveSys);
        if (!NerveSystemQuery.TryComp(nerveSys, out var comp))
            return;

        CleanupSounds(comp);
    }

    private void CleanupSounds(NerveSystemComponent nerveSys)
    {
        foreach (var (id, _) in nerveSys.PlayedPainSounds.Where(sound => !TerminatingOrDeleted(sound.Key)))
        {
            IHaveNoMouthAndIMustScream.Stop(id);
            nerveSys.PlayedPainSounds.Remove(id);
        }

        foreach (var id in nerveSys.PainSoundsToPlay.ToList())
        {
            nerveSys.PainSoundsToPlay.Remove(id.Key);
        }
    }

    private Entity<AudioComponent>? PlayPainSound(SoundSpecifier audio, EntityUid? source, AudioParams? @params)
    {
        var audioParams =
            @params?.AddVolume(Math.Max(MinimalVolume, SharedAudioSystem.GainToVolume(_volume)))
            ?? AudioParams.Default.WithVolume(Math.Max(MinimalVolume, SharedAudioSystem.GainToVolume(_volume)));

        return source.HasValue
            ? IHaveNoMouthAndIMustScream.PlayEntity(audio, Filter.Local(), source.Value, true, audioParams)
            : IHaveNoMouthAndIMustScream.PlayGlobal(audio, Filter.Local(), true, audioParams);
    }

    public override void CleanupPainSounds(EntityUid ent, NerveSystemComponent? nerveSys = null)
    {
        if (!NerveSystemQuery.Resolve(ent, ref nerveSys))
            return;

        CleanupSounds(nerveSys);
    }

    public override Entity<AudioComponent>? PlayPainSound(EntityUid body, SoundSpecifier specifier, AudioParams? audioParams = null)
    {
        return PlayPainSound(specifier, body, audioParams);
    }

    public override Entity<AudioComponent>? PlayPainSound(
        EntityUid body,
        EntityUid nerveSysEnt,
        SoundSpecifier specifier,
        AudioParams? audioParams = null,
        NerveSystemComponent? nerveSys = null)
    {
        if (!NerveSystemQuery.Resolve(nerveSysEnt, ref nerveSys))
            return null;

        var sound = PlayPainSound(body, specifier, audioParams);
        if (!sound.HasValue)
            return null;

        nerveSys.PlayedPainSounds.Add(sound.Value.Owner, sound.Value.Comp);
        return sound.Value;
    }
}
