using Content.Shared.Audio.Jukebox;
using Content.Shared.Backmen.CCVar;
using Robust.Client.Animations;
using Robust.Client.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client.Audio.Jukebox;

public sealed class JukeboxSystem : SharedJukeboxSystem
{
    [Dependency] private readonly IPrototypeManager _protoManager = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly AnimationPlayerSystem _animationPlayer = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearanceSystem = default!;
    [Dependency] private readonly SharedUserInterfaceSystem _uiSystem = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;

    // backmen edit start
    /// <summary>
    /// The volume at which the boombox won't be heard
    /// </summary>
    private const float MinimalVolume = -30f;

    private float _volume;
    // backmen edit end

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<JukeboxComponent, AppearanceChangeEvent>(OnAppearanceChange);
        SubscribeLocalEvent<JukeboxComponent, AnimationCompletedEvent>(OnAnimationCompleted);

        SubscribeNetworkEvent<JukeboxPlaySongEvent>(OnSongPlay);
        SubscribeNetworkEvent<JukeboxPauseSongEvent>(OnSongPause);
        SubscribeNetworkEvent<JukeboxStopSongEvent>(OnSongStop);
        SubscribeNetworkEvent<JukeboxSetPlaybackEvent>(OnSetPlaybackPosition);

        _protoManager.PrototypesReloaded += OnProtoReload;

        Subs.CVar(_cfg, CCVars.BoomboxVolume, OnBoomboxValueChanged, true); // backmen edit
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _protoManager.PrototypesReloaded -= OnProtoReload;
    }

    private void OnProtoReload(PrototypesReloadedEventArgs obj)
    {
        if (!obj.WasModified<JukeboxPrototype>())
            return;

        var query = AllEntityQuery<JukeboxComponent, UserInterfaceComponent>();
        while (query.MoveNext(out var uid, out _, out var ui))
        {
            if (!_uiSystem.TryGetOpenUi<JukeboxBoundUserInterface>((uid, ui), JukeboxUiKey.Key, out var bui))
                continue;

            bui.PopulateMusic();
        }
    }

    // backmen edit start
    public void UpdateJukeboxUi(Entity<JukeboxComponent> ent)
    {
        if (!_uiSystem.TryGetOpenUi<JukeboxBoundUserInterface>(ent.Owner, JukeboxUiKey.Key, out var bui))
            return;

        bui.Reload();
    }
    // backmen edit end

    private void OnAnimationCompleted(EntityUid uid, JukeboxComponent component, AnimationCompletedEvent args)
    {
        if (!TryComp<SpriteComponent>(uid, out var sprite))
            return;

        if (!TryComp<AppearanceComponent>(uid, out var appearance) ||
            !_appearanceSystem.TryGetData<JukeboxVisualState>(uid, JukeboxVisuals.VisualState, out var visualState, appearance))
        {
            visualState = JukeboxVisualState.On;
        }

        UpdateAppearance((uid, sprite), visualState, component);
    }

    private void OnAppearanceChange(EntityUid uid, JukeboxComponent component, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        if (!args.AppearanceData.TryGetValue(JukeboxVisuals.VisualState, out var visualStateObject) ||
            visualStateObject is not JukeboxVisualState visualState)
        {
            visualState = JukeboxVisualState.On;
        }

        UpdateAppearance((uid, args.Sprite), visualState, component);
    }

    private void UpdateAppearance(Entity<SpriteComponent> entity, JukeboxVisualState visualState, JukeboxComponent component)
    {
        SetLayerState(JukeboxVisualLayers.Base, component.OffState, entity);

        switch (visualState)
        {
            case JukeboxVisualState.On:
                SetLayerState(JukeboxVisualLayers.Base, component.OnState, entity);
                break;

            case JukeboxVisualState.Off:
                SetLayerState(JukeboxVisualLayers.Base, component.OffState, entity);
                break;

            case JukeboxVisualState.Select:
                PlayAnimation(entity.Owner, JukeboxVisualLayers.Base, component.SelectState, 1.0f, entity);
                break;
        }
    }

    private void PlayAnimation(EntityUid uid, JukeboxVisualLayers layer, string? state, float animationTime, SpriteComponent sprite)
    {
        if (string.IsNullOrEmpty(state))
            return;

        if (!_animationPlayer.HasRunningAnimation(uid, state))
        {
            var animation = GetAnimation(layer, state, animationTime);
            _sprite.LayerSetVisible((uid, sprite), layer, true);
            _animationPlayer.Play(uid, animation, state);
        }
    }

    private static Animation GetAnimation(JukeboxVisualLayers layer, string state, float animationTime)
    {
        return new Animation
        {
            Length = TimeSpan.FromSeconds(animationTime),
            AnimationTracks =
                {
                    new AnimationTrackSpriteFlick
                    {
                        LayerKey = layer,
                        KeyFrames =
                        {
                            new AnimationTrackSpriteFlick.KeyFrame(state, 0f)
                        }
                    }
                }
        };
    }

    private void SetLayerState(JukeboxVisualLayers layer, string? state, Entity<SpriteComponent> sprite)
    {
        if (string.IsNullOrEmpty(state))
            return;

        _sprite.LayerSetVisible(sprite.AsNullable(), layer, true);
        _sprite.LayerSetAutoAnimated(sprite.AsNullable(), layer, true);
        _sprite.LayerSetRsiState(sprite.AsNullable(), layer, state);
    }

    // backmen edit start
    private void OnBoomboxValueChanged(float volume)
    {
        _volume = volume;

        var q = EntityQueryEnumerator<JukeboxComponent>();
        while (q.MoveNext(out _, out var component))
        {
            if (component.AudioStream != null)
                Audio.SetGain(component.AudioStream, Math.Max(MinimalVolume, _volume));
        }
    }

    private void OnSongPlay(JukeboxPlaySongEvent ev)
    {
        if (!TryGetEntity(ev.Jukebox, out var ent))
            return;

        if (!TryComp<JukeboxComponent>(ent, out var component))
            return;

        if (component.AudioStream is { } audio && Comp<AudioComponent>(audio).State == AudioState.Paused)
        {
            Audio.SetState(component.AudioStream, AudioState.Playing);
        }
        else
        {
            component.AudioStream = Audio.Stop(component.AudioStream);

            if (string.IsNullOrEmpty(component.SelectedSongId) ||
                !_protoManager.TryIndex(component.SelectedSongId, out var jukeboxProto))
            {
                return;
            }

            component.AudioStream =
                Audio.PlayPvs(
                        jukeboxProto.Path,
                        ent.Value,
                        AudioParams.Default.WithMaxDistance(10f).WithVolume(Math.Max(MinimalVolume, SharedAudioSystem.GainToVolume(_volume))))
                    ?.Entity;
        }

        UpdateJukeboxUi((ent.Value, component));
    }

    private void OnSongPause(JukeboxPauseSongEvent ev)
    {
        if (!TryGetEntity(ev.Jukebox, out var ent))
            return;

        if (!TryComp<JukeboxComponent>(ent, out var component))
            return;

        Audio.SetState(component.AudioStream, AudioState.Paused);
    }

    private void OnSongStop(JukeboxStopSongEvent ev)
    {
        if (!TryGetEntity(ev.Jukebox, out var ent))
            return;

        if (!TryComp<JukeboxComponent>(ent, out var component))
            return;

        Audio.SetState(component.AudioStream, AudioState.Stopped);
        UpdateJukeboxUi((ent.Value, component));
    }

    private void OnSetPlaybackPosition(JukeboxSetPlaybackEvent ev)
    {
        if (!TryGetEntity(ev.Jukebox, out var ent))
            return;

        if (!TryComp<JukeboxComponent>(ent, out var component))
            return;

        Audio.SetPlaybackPosition(component.AudioStream, ev.Position);
    }
    // backmen edit end
}
