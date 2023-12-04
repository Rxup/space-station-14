using Content.Shared.GameTicking;
using Content.Shared.Backmen.Eye.NightVision.Components;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Player;

namespace Content.Client.GG.Eye.NightVision;

public sealed class NightVisionSystem : EntitySystem
{
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IOverlayManager _overlayMan = default!;
    [Dependency] private readonly ILightManager _lightManager = default!;


    private NightVisionOverlay _overlay = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NightVisionComponent, ComponentInit>(OnNightVisionInit);
        SubscribeLocalEvent<NightVisionComponent, ComponentShutdown>(OnNightVisionShutdown);

        SubscribeLocalEvent<NightVisionComponent, LocalPlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<NightVisionComponent, LocalPlayerDetachedEvent>(OnPlayerDetached);

        SubscribeNetworkEvent<RoundRestartCleanupEvent>(RoundRestartCleanup);
    }

    private void OnPlayerAttached(EntityUid uid, NightVisionComponent component, LocalPlayerAttachedEvent args)
    {
    	_overlay = new(component.NightVisionColor);
        _overlayMan.AddOverlay(_overlay);
    }

    private void OnPlayerDetached(EntityUid uid, NightVisionComponent component, LocalPlayerDetachedEvent args)
    {
        _overlay = new(component.NightVisionColor);
        _overlayMan.RemoveOverlay(_overlay);
        _lightManager.DrawLighting = true;
    }

    private void OnNightVisionInit(EntityUid uid, NightVisionComponent component, ComponentInit args)
    {
	    _overlay = new(component.NightVisionColor);
        if (_player.LocalPlayer?.ControlledEntity == uid)
            _overlayMan.AddOverlay(_overlay);
    }

    private void OnNightVisionShutdown(EntityUid uid, NightVisionComponent component, ComponentShutdown args)
    {
        _overlay = new(component.NightVisionColor);
        if (_player.LocalPlayer?.ControlledEntity == uid)
        {
            _lightManager.DrawLighting = true;
            _overlayMan.RemoveOverlay(_overlay);
        }
    }

    private void RoundRestartCleanup(RoundRestartCleanupEvent ev)
    {
        _lightManager.DrawLighting = true;
    }
}
