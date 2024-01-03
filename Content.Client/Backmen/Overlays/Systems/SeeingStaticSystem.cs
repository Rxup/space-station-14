using Content.Client.Backmen.Overlays.Shaders;
using Content.Shared.Backmen.Silicon.Components;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Player;

namespace Content.Client.Backmen.Silicon.Systems;

/// <summary>
///     System to handle the SeeingStatic overlay.
/// </summary>
public sealed class SeeingStaticSystem : EntitySystem
{
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IOverlayManager _overlayMan = default!;

    private StaticOverlay _overlay = default!;
    public const string StaticKey = "SeeingStatic";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SeeingStaticComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<SeeingStaticComponent, ComponentShutdown>(OnShutdown);

        SubscribeLocalEvent<SeeingStaticComponent, PlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<SeeingStaticComponent, PlayerDetachedEvent>(OnPlayerDetached);

        _overlay = new();
    }

    private void OnPlayerAttached(EntityUid uid, SeeingStaticComponent component, PlayerAttachedEvent args)
    {
        _overlayMan.AddOverlay(_overlay);
    }

    private void OnPlayerDetached(EntityUid uid, SeeingStaticComponent component, PlayerDetachedEvent args)
    {
        _overlay.MixAmount = 0;
        _overlayMan.RemoveOverlay(_overlay);
    }

    private void OnInit(EntityUid uid, SeeingStaticComponent component, ComponentInit args)
    {
        if (_player.LocalPlayer?.ControlledEntity == uid)
            _overlayMan.AddOverlay(_overlay);
    }

    private void OnShutdown(EntityUid uid, SeeingStaticComponent component, ComponentShutdown args)
    {
        if (_player.LocalPlayer?.ControlledEntity == uid)
        {
            _overlay.MixAmount = 0;
            _overlayMan.RemoveOverlay(_overlay);
        }
    }
}
