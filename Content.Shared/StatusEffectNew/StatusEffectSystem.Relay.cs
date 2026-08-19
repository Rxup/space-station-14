using Content.Shared.Backmen.Eye.NightVision.Systems;
using Content.Shared.Body.Events;
using Content.Shared.Backmen.Surgery.Wounds;
using Content.Shared.Chat; // backmen: muted-status-effects
using Content.Shared.Damage;
using Content.Shared.Damage.Events;
using Content.Shared.Damage.Systems;
using Content.Shared.Eye.Blinding.Systems;
using Content.Shared.Flash;
using Content.Shared.HealthExaminable; // backmen: pain-immune-status
using Content.Shared.Mobs;
using Content.Shared.Mobs.Events;
using Content.Shared.Movement.Events;
using Content.Shared.Movement.Systems;
using Content.Shared.Rejuvenate;
using Content.Shared.Speech;
using Content.Shared.StatusEffectNew.Components;
using Content.Shared.Stunnable;
using Robust.Shared.Player;

namespace Content.Shared.StatusEffectNew;

public sealed partial class StatusEffectsSystem
{
    private void InitializeRelay()
    {
        SubscribeLocalEvent<StatusEffectContainerComponent, LocalPlayerAttachedEvent>(RelayStatusEffectEvent);
        SubscribeLocalEvent<StatusEffectContainerComponent, LocalPlayerDetachedEvent>(RelayStatusEffectEvent);
        SubscribeLocalEvent<StatusEffectContainerComponent, RejuvenateEvent>(RelayStatusEffectEvent);

        SubscribeLocalEvent<StatusEffectContainerComponent, RefreshMovementSpeedModifiersEvent>(RelayStatusEffectEvent);
        SubscribeLocalEvent<StatusEffectContainerComponent, UpdateCanMoveEvent>(RelayStatusEffectEvent);

        SubscribeLocalEvent<StatusEffectContainerComponent, RefreshFrictionModifiersEvent>(RefRelayStatusEffectEvent);
        SubscribeLocalEvent<StatusEffectContainerComponent, TileFrictionEvent>(RefRelayStatusEffectEvent);

        SubscribeLocalEvent<StatusEffectContainerComponent, StandUpAttemptEvent>(RefRelayStatusEffectEvent);
        SubscribeLocalEvent<StatusEffectContainerComponent, StunEndAttemptEvent>(RefRelayStatusEffectEvent);
        SubscribeLocalEvent<StatusEffectContainerComponent, RefreshStaminaCritThresholdEvent>(RefRelayStatusEffectEvent);
        SubscribeLocalEvent<StatusEffectContainerComponent, CanSeeAttemptEvent>(RelayStatusEffectEvent);
        SubscribeLocalEvent<StatusEffectContainerComponent, FlashAttemptEvent>(RefRelayStatusEffectEvent);

        SubscribeLocalEvent<StatusEffectContainerComponent, BeforeForceSayEvent>(RelayStatusEffectEvent);
        SubscribeLocalEvent<StatusEffectContainerComponent, BeforeAlertSeverityCheckEvent>(RelayStatusEffectEvent);
        // start-backmen: muted-status-effects
        SubscribeLocalEvent<StatusEffectContainerComponent, SpeakAttemptEvent>(RelayStatusEffectEvent);
        SubscribeLocalEvent<StatusEffectContainerComponent, EmoteActionEvent>(RelayStatusEffectEvent);
        SubscribeLocalEvent<StatusEffectContainerComponent, EmoteEvent>(RefRelayStatusEffectEvent);
        // end-backmen: muted-status-effects

        SubscribeLocalEvent<StatusEffectContainerComponent, AccentGetEvent>(RelayStatusEffectEvent);

        SubscribeLocalEvent<StatusEffectContainerComponent, BleedModifierEvent>(RefRelayStatusEffectEvent);
        SubscribeLocalEvent<StatusEffectContainerComponent, DamageModifyEvent>(RelayStatusEffectEvent);
        SubscribeLocalEvent<StatusEffectContainerComponent, DamageChangedEvent>(RelayStatusEffectEvent); // backmen: psionic invis
        SubscribeLocalEvent<StatusEffectContainerComponent, WoundsChangedEvent>(RefRelayStatusEffectEvent); // backmen: psionic invis

        SubscribeLocalEvent<StatusEffectContainerComponent, CanVisionAttemptEvent>(RelayStatusEffectEvent); // backmen
        SubscribeLocalEvent<StatusEffectContainerComponent, MobStateChangedEvent>(RefRelayStatusEffectEvent); // backmen
        SubscribeLocalEvent<StatusEffectContainerComponent, HealthBeingExaminedEvent>(RelayStatusEffectEvent); // backmen: pain-immune-status
    }

    private void RefRelayStatusEffectEvent<T>(EntityUid uid, StatusEffectContainerComponent component, ref T args)
        where T : struct
    {
        RelayEvent((uid, component), ref args);
    }

    private void RelayStatusEffectEvent<T>(EntityUid uid, StatusEffectContainerComponent component, T args)
        where T : class
    {
        RelayEvent((uid, component), args);
    }

    public void RelayEvent<T>(Entity<StatusEffectContainerComponent> statusEffect, ref T args) where T : struct
    {
        // this copies the by-ref event if it is a struct
        var ev = new StatusEffectRelayedEvent<T>(args);
        foreach (var activeEffect in statusEffect.Comp.ActiveStatusEffects?.ContainedEntities ?? [])
        {
            RaiseLocalEvent(activeEffect, ref ev);
        }

        // and now we copy it back
        args = ev.Args;
    }

    public void RelayEvent<T>(Entity<StatusEffectContainerComponent> statusEffect, T args) where T : class
    {
        // this copies the by-ref event if it is a struct
        var ev = new StatusEffectRelayedEvent<T>(args);
        foreach (var activeEffect in statusEffect.Comp.ActiveStatusEffects?.ContainedEntities ?? [])
        {
            RaiseLocalEvent(activeEffect, ref ev);
        }
    }
}

/// <summary>
/// Event wrapper for relayed events.
/// </summary>
[ByRefEvent]
public record struct StatusEffectRelayedEvent<TEvent>(TEvent Args);
