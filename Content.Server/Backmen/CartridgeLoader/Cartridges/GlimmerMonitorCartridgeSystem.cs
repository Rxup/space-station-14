﻿using Content.Server.Backmen.Psionics.Glimmer;
using Content.Server.CartridgeLoader;
using Content.Shared.Backmen.CartridgeLoader;
using Content.Shared.Backmen.CartridgeLoader.Cartridges;
using Content.Shared.CartridgeLoader;

namespace Content.Server.Backmen.CartridgeLoader.Cartridges;

public sealed class GlimmerMonitorCartridgeSystem : EntitySystem
{
    [Dependency] private readonly CartridgeLoaderSystem? _cartridgeLoaderSystem = default!;
    [Dependency] private readonly PassiveGlimmerReductionSystem _glimmerReductionSystem = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<GlimmerMonitorCartridgeComponent, CartridgeUiReadyEvent>(OnUiReady);
        SubscribeLocalEvent<GlimmerMonitorCartridgeComponent, CartridgeMessageEvent>(OnMessage);
    }

    /// <summary>
    /// This gets called when the ui fragment needs to be updated for the first time after activating
    /// </summary>
    private void OnUiReady(EntityUid uid, GlimmerMonitorCartridgeComponent component, CartridgeUiReadyEvent args)
    {
        UpdateUiState(uid, args.Loader, component);
    }

    private void OnMessage(EntityUid uid, GlimmerMonitorCartridgeComponent component, CartridgeMessageEvent args)
    {
        if (args is not GlimmerMonitorSyncMessageEvent)
            return;

        UpdateUiState(uid, args.LoaderUid, component);
    }

    public void UpdateUiState(EntityUid uid, EntityUid loaderUid, GlimmerMonitorCartridgeComponent? component)
    {
        if (!Resolve(uid, ref component))
            return;

        var state = new GlimmerMonitorUiState(_glimmerReductionSystem.GlimmerValues);
        _cartridgeLoaderSystem?.UpdateCartridgeUiState(loaderUid, state);
    }
}
