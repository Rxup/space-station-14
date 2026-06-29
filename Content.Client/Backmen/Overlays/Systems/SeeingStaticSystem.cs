using Content.Client.Backmen.Overlays.Shaders;
using Content.Shared.Backmen.Silicon.Components;
using Content.Shared.StatusEffectNew;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Player;

namespace Content.Client.Backmen.Silicon.Systems;

/// <summary>
///     System to handle the SeeingStatic overlay.
/// </summary>
public sealed partial class SeeingStaticSystem : EntitySystem
{
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IOverlayManager _overlayMan = default!;
    [Dependency] private StatusEffectsSystem _statusEffects = default!;

    private StaticOverlay _overlay = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SeeingStaticComponent, StatusEffectAppliedEvent>(OnApplied);
        SubscribeLocalEvent<SeeingStaticComponent, StatusEffectRemovedEvent>(OnRemoved);

        SubscribeLocalEvent<SeeingStaticComponent, StatusEffectRelayedEvent<LocalPlayerAttachedEvent>>(OnPlayerAttached);
        SubscribeLocalEvent<SeeingStaticComponent, StatusEffectRelayedEvent<LocalPlayerDetachedEvent>>(OnPlayerDetached);

        _overlay = new();
    }

    private void OnApplied(Entity<SeeingStaticComponent> ent, ref StatusEffectAppliedEvent args)
    {
        if (_player.LocalEntity == args.Target)
            _overlayMan.AddOverlay(_overlay);
    }

    private void OnRemoved(Entity<SeeingStaticComponent> ent, ref StatusEffectRemovedEvent args)
    {
        if (_player.LocalEntity != args.Target)
            return;

        if (!_statusEffects.HasEffectComp<SeeingStaticComponent>(_player.LocalEntity.Value))
        {
            _overlay.MixAmount = 0;
            _overlayMan.RemoveOverlay(_overlay);
        }
    }

    private void OnPlayerAttached(Entity<SeeingStaticComponent> ent, ref StatusEffectRelayedEvent<LocalPlayerAttachedEvent> args)
    {
        _overlayMan.AddOverlay(_overlay);
    }

    private void OnPlayerDetached(Entity<SeeingStaticComponent> ent, ref StatusEffectRelayedEvent<LocalPlayerDetachedEvent> args)
    {
        if (_player.LocalEntity is null)
            return;

        if (!_statusEffects.HasEffectComp<SeeingStaticComponent>(_player.LocalEntity.Value))
        {
            _overlay.MixAmount = 0;
            _overlayMan.RemoveOverlay(_overlay);
        }
    }
}
