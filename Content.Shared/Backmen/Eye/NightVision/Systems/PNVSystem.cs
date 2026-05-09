using Content.Shared.Backmen.Eye.NightVision.Components;
using Content.Shared.Inventory;
using Content.Shared.Actions;
using Content.Shared.Clothing.Components;
using Content.Shared.Inventory.Events;
using Content.Shared.StatusEffectNew;
using Content.Shared.StatusEffectNew.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;

namespace Content.Shared.Backmen.Eye.NightVision.Systems;

public sealed partial class PNVSystem : EntitySystem
{
    [Dependency] private NightVisionSystem _nightvisionableSystem = default!;
    [Dependency] private SharedActionsSystem _actionsSystem = default!;
    [Dependency] private SharedAudioSystem _audioSystem = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private StatusEffectsSystem _statusEffects = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PNVComponent, GotEquippedEvent>(OnEquipped);
        SubscribeLocalEvent<PNVComponent, GotUnequippedEvent>(OnUnequipped);
        SubscribeLocalEvent<PNVStatusEffectComponent, StatusEffectAppliedEvent>(OnPNVApplied);
        SubscribeLocalEvent<PNVStatusEffectComponent, StatusEffectRemovedEvent>(OnPNVRemoved);
        SubscribeLocalEvent<PNVStatusEffectComponent, StatusEffectRelayedEvent<CanVisionAttemptEvent>>(OnPNVTrySee);
    }

    private void OnPNVTrySee(EntityUid uid, PNVStatusEffectComponent component, ref StatusEffectRelayedEvent<CanVisionAttemptEvent> args)
    {
        args.Args.Cancel();
    }

    private void OnPNVApplied(EntityUid uid, PNVStatusEffectComponent component, ref StatusEffectAppliedEvent args)
    {
        if (!_net.IsServer)
            return;

        var nvComp = EnsureComp<NightVisionComponent>(args.Target);
        nvComp.IsGranted = true;

        _nightvisionableSystem.UpdateIsNightVision(args.Target, nvComp);
        _actionsSystem.AddAction(args.Target, ref component.ActionContainer, component.ActionProto);
        nvComp.ActionContainer = component.ActionContainer;
        _actionsSystem.SetCooldown(component.ActionContainer, TimeSpan.FromSeconds(1)); // GCD?

        if (!nvComp.PlaySoundOn)
            return;

        _audioSystem.PlayPredicted(nvComp.OnOffSound, args.Target, args.Target);
    }

    private void OnPNVRemoved(EntityUid uid, PNVStatusEffectComponent component, ref StatusEffectRemovedEvent args)
    {
        if (!_net.IsServer)
            return;

        if (_statusEffects.HasEffectComp<PNVStatusEffectComponent>(args.Target))
            return;

        if (!TryComp<NightVisionComponent>(args.Target, out var nvComp) || !nvComp.IsGranted)
            return;

        _nightvisionableSystem.UpdateIsNightVision(args.Target, nvComp);
        _actionsSystem.RemoveAction(args.Target, component.ActionContainer);
        nvComp.ActionContainer = null;
        RemCompDeferred<NightVisionComponent>(args.Target);
    }

    private void OnEquipped(EntityUid uid, PNVComponent component, GotEquippedEvent args)
    {
        if (!TryComp<ClothingComponent>(uid, out var clothing))
            return;

        if (!clothing.Slots.HasFlag(args.SlotFlags))
            return;

        if (!_statusEffects.TrySetStatusEffectDuration(args.Equipee, component.StatusEffect))
            return;

        component._hasEffect = true;
    }

    private void OnUnequipped(EntityUid uid, PNVComponent component, GotUnequippedEvent args)
    {
        if (!TryComp<ClothingComponent>(uid, out var clothing))
            return;

        if (!clothing.Slots.HasFlag(args.SlotFlags))
            return;

        if (!component._hasEffect)
            return;

        component._hasEffect = false;
        _statusEffects.TryRemoveStatusEffect(args.Equipee, component.StatusEffect);
    }
}
