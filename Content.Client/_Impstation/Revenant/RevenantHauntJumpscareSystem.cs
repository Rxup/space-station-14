using System.Numerics;
using Content.Shared.CCVar;
using Content.Shared.Revenant;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;
using Robust.Shared.Maths;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client._Impstation.Revenant;

/// <summary>
/// Shows a brief fullscreen revenant jumpscare when the local player is haunted.
/// </summary>
public sealed partial class RevenantHauntJumpscareSystem : EntitySystem
{
    [Dependency] private IOverlayManager _overlay = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IPlayerManager _player = default!;

    private RevenantHauntJumpscareOverlay _jumpscare = default!;

    public override void Initialize()
    {
        base.Initialize();

        _jumpscare = new RevenantHauntJumpscareOverlay();
        SubscribeNetworkEvent<RevenantHauntJumpscareEvent>(OnJumpscare);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _overlay.RemoveOverlay(_jumpscare);
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        if (!_overlay.HasOverlay<RevenantHauntJumpscareOverlay>())
            return;

        if (_jumpscare.IsActive(_timing.CurTime))
            return;

        _overlay.RemoveOverlay(_jumpscare);
    }

    private void OnJumpscare(RevenantHauntJumpscareEvent args)
    {
        if (_player.LocalEntity == null)
            return;

        // Reduced motion: skip the face popup; flash/stun from Haunt still apply.
        if (_cfg.GetCVar(CCVars.ReducedMotion))
            return;

        _jumpscare.Begin(_timing.CurTime, args.Duration <= TimeSpan.Zero
            ? TimeSpan.FromSeconds(1.1)
            : args.Duration);

        if (!_overlay.HasOverlay<RevenantHauntJumpscareOverlay>())
            _overlay.AddOverlay(_jumpscare);
    }
}

public sealed partial class RevenantHauntJumpscareOverlay : Overlay
{
    private static readonly ResPath RevenantRsi = new("/Textures/Mobs/Ghosts/revenant.rsi");

    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private Robust.Client.GameObjects.SpriteSystem _sprite = default!;

    private TimeSpan _start;
    private TimeSpan _duration = TimeSpan.FromSeconds(1.1);
    private Texture? _face;

    public override OverlaySpace Space => OverlaySpace.ScreenSpace;

    public RevenantHauntJumpscareOverlay()
    {
        IoCManager.InjectDependencies(this);
    }

    public void Begin(TimeSpan start, TimeSpan duration)
    {
        _start = start;
        _duration = duration;
        _face ??= _sprite.Frame0(new SpriteSpecifier.Rsi(RevenantRsi, "active"));
    }

    public bool IsActive(TimeSpan now) => _face != null && now - _start < _duration;

    protected override bool BeforeDraw(in OverlayDrawArgs args) => IsActive(_timing.CurTime);

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (_face == null)
            return;

        var elapsed = _timing.CurTime - _start;
        var t = Math.Clamp((float)(elapsed.TotalSeconds / _duration.TotalSeconds), 0f, 1f);

        // Hard hit, then quick fade.
        var alpha = t < 0.35f
            ? 1f
            : 1f - MathF.Pow((t - 0.35f) / 0.65f, 1.6f);

        var handle = args.ScreenHandle;
        UIBox2 viewport = args.ViewportBounds;

        handle.DrawRect(viewport, Color.Black.WithAlpha(0.92f * alpha));

        // Huge centered face (classic jumpscare framing).
        var size = Math.Min(viewport.Width, viewport.Height) * 0.85f;
        var center = viewport.Center;
        var topLeft = center - new Vector2(size * 0.5f, size * 0.5f);
        var box = UIBox2.FromDimensions(topLeft, new Vector2(size, size));

        // Slight shake early on.
        if (t < 0.45f)
        {
            var shake = (1f - t / 0.45f) * size * 0.03f;
            var ox = MathF.Sin((float)_timing.CurTime.TotalSeconds * 70f) * shake;
            var oy = MathF.Cos((float)_timing.CurTime.TotalSeconds * 85f) * shake;
            box = box.Translated(new Vector2(ox, oy));
        }

        handle.DrawTextureRect(_face, box, Color.FromHex("#FF2020").WithAlpha(alpha));
    }
}
