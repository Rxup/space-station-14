using Content.Shared.Backmen.Explosion.Components;
using Content.Shared.StatusEffectNew;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Player;

namespace Content.Client.Backmen.Explosion;

public sealed partial class RMCExplosionShockWaveOverlaySystem : EntitySystem
{
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IOverlayManager _overlayMan = default!;
    [Dependency] private StatusEffectsSystem _statusEffects = default!;

    private RMCExplosionShockWaveOverlay _overlay = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ShockWaveStatusEffectComponent, StatusEffectAppliedEvent>(OnApplied);
        SubscribeLocalEvent<ShockWaveStatusEffectComponent, StatusEffectRemovedEvent>(OnRemoved);
        SubscribeLocalEvent<ShockWaveStatusEffectComponent, StatusEffectRelayedEvent<LocalPlayerAttachedEvent>>(OnPlayerAttached);
        SubscribeLocalEvent<ShockWaveStatusEffectComponent, StatusEffectRelayedEvent<LocalPlayerDetachedEvent>>(OnPlayerDetached);

        _overlay = new();
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _overlayMan.RemoveOverlay(_overlay);
    }

    private void OnApplied(Entity<ShockWaveStatusEffectComponent> ent, ref StatusEffectAppliedEvent args)
    {
        if (_player.LocalEntity != args.Target)
            return;

        if (!_overlayMan.HasOverlay<RMCExplosionShockWaveOverlay>())
            _overlayMan.AddOverlay(_overlay);
    }

    private void OnRemoved(Entity<ShockWaveStatusEffectComponent> ent, ref StatusEffectRemovedEvent args)
    {
        if (_player.LocalEntity != args.Target)
            return;

        if (!_statusEffects.HasEffectComp<ShockWaveStatusEffectComponent>(args.Target))
            _overlayMan.RemoveOverlay(_overlay);
    }

    private void OnPlayerAttached(Entity<ShockWaveStatusEffectComponent> ent, ref StatusEffectRelayedEvent<LocalPlayerAttachedEvent> args)
    {
        if (!_overlayMan.HasOverlay<RMCExplosionShockWaveOverlay>())
            _overlayMan.AddOverlay(_overlay);
    }

    private void OnPlayerDetached(Entity<ShockWaveStatusEffectComponent> ent, ref StatusEffectRelayedEvent<LocalPlayerDetachedEvent> args)
    {
        if (_player.LocalEntity is null || _statusEffects.HasEffectComp<ShockWaveStatusEffectComponent>(_player.LocalEntity.Value))
            return;

        _overlayMan.RemoveOverlay(_overlay);
    }
}
