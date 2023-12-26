using Robust.Client.Graphics;
using Robust.Client.Player;
using Content.Client.Backmen.Overlays;
using Content.Client.Backmen.Overlays.Shaders;
using Content.Shared.Backmen.Species.Shadowkin.Components;
using Robust.Client.GameObjects;
using Content.Shared.Humanoid;
using Robust.Shared.Player;

namespace Content.Client.Backmen.Species.Shadowkin.Systems;

public sealed class ShadowkinDarkSwappedSystem : EntitySystem
{
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IOverlayManager _overlay = default!;

    private IgnoreHumanoidWithComponentOverlay _ignoreOverlay = default!;
    private EtherealOverlay _etherealOverlay = default!;

    public override void Initialize()
    {
        base.Initialize();

        _ignoreOverlay = new IgnoreHumanoidWithComponentOverlay();
        _ignoreOverlay.IgnoredComponents.Add(new HumanoidAppearanceComponent());
        _ignoreOverlay.AllowAnywayComponents.Add(new ShadowkinDarkSwappedComponent());
        _etherealOverlay = new EtherealOverlay();

        SubscribeLocalEvent<ShadowkinDarkSwappedComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<ShadowkinDarkSwappedComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<ShadowkinDarkSwappedComponent, PlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<ShadowkinDarkSwappedComponent, PlayerDetachedEvent>(OnPlayerDetached);
    }


    private void OnStartup(EntityUid uid, ShadowkinDarkSwappedComponent component, ComponentStartup args)
    {
        if (_player.LocalSession?.AttachedEntity != uid)
            return;

        AddOverlay();
    }

    private void OnShutdown(EntityUid uid, ShadowkinDarkSwappedComponent component, ComponentShutdown args)
    {
        if (_player.LocalSession?.AttachedEntity != uid)
            return;

        RemoveOverlay();
    }

    private void OnPlayerAttached(EntityUid uid, ShadowkinDarkSwappedComponent component, PlayerAttachedEvent args)
    {
        AddOverlay();
    }

    private void OnPlayerDetached(EntityUid uid, ShadowkinDarkSwappedComponent component, PlayerDetachedEvent args)
    {
        RemoveOverlay();
    }


    private void AddOverlay()
    {
        _overlay.AddOverlay(_ignoreOverlay);
        _overlay.AddOverlay(_etherealOverlay);
    }

    private void RemoveOverlay()
    {
        _ignoreOverlay.Reset();
        _overlay.RemoveOverlay(_ignoreOverlay);
        _overlay.RemoveOverlay(_etherealOverlay);
    }
}
