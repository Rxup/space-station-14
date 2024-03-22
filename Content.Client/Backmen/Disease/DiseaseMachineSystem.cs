﻿using Content.Client.Medical;
using Content.Shared.Backmen.Disease;
using Robust.Client.GameObjects;

namespace Content.Client.Backmen.Disease;

/// <summary>
/// Controls client-side visuals for the
/// disease machines.
/// </summary>
public sealed class DiseaseMachineSystem : VisualizerSystem<DiseaseMachineVisualsComponent>
{
    protected override void OnAppearanceChange(EntityUid uid, DiseaseMachineVisualsComponent component, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        if (AppearanceSystem.TryGetData<bool>(uid, DiseaseMachineVisuals.IsOn, out var isOn, args.Component)
            && AppearanceSystem.TryGetData<bool>(uid, DiseaseMachineVisuals.IsRunning, out var isRunning, args.Component))
        {
            var state = isRunning ? component.RunningState : component.IdleState;
            args.Sprite.LayerSetVisible(DiseaseMachineVisualLayers.IsOn, isOn);
            args.Sprite.LayerSetState(DiseaseMachineVisualLayers.IsRunning, state);
        }
    }
}
