using Robust.Client.Graphics;
using Robust.Client.Player;
using Content.Client.Backmen.Overlays;
using Content.Client.Backmen.Overlays.Shaders;
using Content.Shared.Backmen.Species.Shadowkin.Components;
using Robust.Client.GameObjects;
using Content.Shared.GameTicking;
using Content.Shared.Humanoid;
using Robust.Shared.Player;

namespace Content.Client.Backmen.Species.Shadowkin.Systems;

public sealed class ShadowkinTintSystem : EntitySystem
{
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IOverlayManager _overlay = default!;

    private ColorTintOverlay _tintOverlay = default!;

    public override void Initialize()
    {
        base.Initialize();

        _tintOverlay = new ColorTintOverlay
        {
            TintColor = new Vector3(0.5f, 0f, 0.5f),
            TintAmount = 0.25f,
            Comp = new ShadowkinComponent()
        };

        SubscribeLocalEvent<ShadowkinComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<ShadowkinComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);

        _player.LocalPlayerAttached += PlayerOnLocalPlayerAttached;
        _player.LocalPlayerDetached += PlayerOnLocalPlayerDetached;
    }

    public override void Shutdown()
    {
        base.Shutdown();

        _player.LocalPlayerAttached -= PlayerOnLocalPlayerAttached;
        _player.LocalPlayerDetached -= PlayerOnLocalPlayerDetached;
    }

    private void PlayerOnLocalPlayerDetached(EntityUid uid)
    {
        if (_overlay.HasOverlay<ColorTintOverlay>())
            _overlay.RemoveOverlay(_tintOverlay);
    }

    private void PlayerOnLocalPlayerAttached(EntityUid uid)
    {
        if(HasComp<ShadowkinComponent>(uid))
            _overlay.AddOverlay(_tintOverlay);
    }

    private void OnStartup(EntityUid uid, ShadowkinComponent component, ComponentStartup args)
    {
        if (_player.LocalSession?.AttachedEntity != uid)
            return;

        _overlay.AddOverlay(_tintOverlay);
    }

    private void OnShutdown(EntityUid uid, ShadowkinComponent component, ComponentShutdown args)
    {
        if (_player.LocalSession != null && _player.LocalSession?.AttachedEntity != uid)
            return;

        _overlay.RemoveOverlay(_tintOverlay);
    }

    private void OnRoundRestart(RoundRestartCleanupEvent args)
    {
        _overlay.RemoveOverlay(_tintOverlay);
    }


    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var uid = _player.LocalSession?.AttachedEntity;
        if (uid == null ||
            !TryComp(uid, out ShadowkinComponent? comp) ||
            !TryComp(uid, out SpriteComponent? sprite) ||
            !sprite.LayerMapTryGet(HumanoidVisualLayers.Eyes, out var index) ||
            !sprite.TryGetLayer(index, out var layer))
        {
            if (_overlay.HasOverlay<ColorTintOverlay>())
                _overlay.RemoveOverlay(_tintOverlay);
            return;
        }

        // Eye color
        comp.TintColor = new Vector3(layer.Color.R, layer.Color.G, layer.Color.B);

        // 1/3 = 0.333...
        // intensity = min + (power / max)
        // intensity = intensity / 0.333
        // intensity = clamp intensity min, max
        const float min = 0.45f;
        const float max = 0.75f;
        comp.TintIntensity = Math.Clamp(min + (comp.PowerLevel / comp.PowerLevelMax) * 0.333f, min, max);

        UpdateShader(comp.TintColor, comp.TintIntensity);
    }


    private void UpdateShader(Vector3? color, float? intensity)
    {
        while (_overlay.HasOverlay<ColorTintOverlay>())
        {
            _overlay.RemoveOverlay(_tintOverlay);
        }

        if (color != null)
            _tintOverlay.TintColor = color;
        if (intensity != null)
            _tintOverlay.TintAmount = intensity;

        _overlay.AddOverlay(_tintOverlay);
    }
}
