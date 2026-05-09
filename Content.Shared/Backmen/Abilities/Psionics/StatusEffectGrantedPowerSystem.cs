using Content.Shared.Actions;
using Content.Shared.StatusEffectNew;
using Content.Shared.StatusEffectNew.Components;

namespace Content.Shared.Backmen.Abilities.Psionics;

/// <summary>
/// Common lifecycle + relay handling for powers that may be granted by status effects.
/// </summary>
public abstract partial class StatusEffectGrantedPowerSystem<TPowerComponent, TActionEvent> : EntitySystem
    where TPowerComponent : Component
    where TActionEvent : BaseActionEvent
{
    [Dependency] protected StatusEffectsSystem StatusEffects = default!;

    protected void InitializeStatusEffectGrantedPower()
    {
        SubscribeLocalEvent<TPowerComponent, ComponentInit>(OnPowerInit);
        SubscribeLocalEvent<TPowerComponent, StatusEffectAppliedEvent>(OnPowerApplied);
        SubscribeLocalEvent<TPowerComponent, StatusEffectRemovedEvent>(OnPowerRemoved);
        SubscribeLocalEvent<TPowerComponent, StatusEffectRelayedEvent<TActionEvent>>(OnPowerRelayed);
        SubscribeLocalEvent<TPowerComponent, TActionEvent>(OnPowerUsedDirect);
        SubscribeLocalEvent<StatusEffectContainerComponent, TActionEvent>(OnPowerUsedFromStatusEffect);
    }

    private void OnPowerInit(EntityUid uid, TPowerComponent component, ComponentInit args)
    {
        if (HasComp<StatusEffectComponent>(uid))
            return;

        EnsurePowerActions(uid, component);
    }

    private void OnPowerApplied(Entity<TPowerComponent> ent, ref StatusEffectAppliedEvent args)
    {
        EnsurePowerActions(args.Target, ent.Comp);
    }

    private void OnPowerRemoved(Entity<TPowerComponent> ent, ref StatusEffectRemovedEvent args)
    {
        if (HasComp<TPowerComponent>(args.Target) || StatusEffects.HasEffectComp<TPowerComponent>(args.Target))
            return;

        RemovePowerActions(args.Target, ent.Comp);
    }

    private void OnPowerRelayed(Entity<TPowerComponent> ent, ref StatusEffectRelayedEvent<TActionEvent> args)
    {
        HandlePowerUse(ent.Owner, ent.Comp, args.Args);
    }

    private void OnPowerUsedDirect(EntityUid uid, TPowerComponent component, TActionEvent args)
    {
        HandlePowerUse(uid, component, args);
    }

    private void OnPowerUsedFromStatusEffect(EntityUid uid, StatusEffectContainerComponent component, TActionEvent args)
    {
        if (args.Handled)
            return;

        // Permanent powers on the user should process via component-bound subscription.
        if (HasComp<TPowerComponent>(uid))
            return;

        StatusEffects.RelayEvent((uid, component), args);
    }

    protected abstract void EnsurePowerActions(EntityUid uid, TPowerComponent component);
    protected abstract void RemovePowerActions(EntityUid uid, TPowerComponent component);
    protected abstract void HandlePowerUse(EntityUid uid, TPowerComponent component, TActionEvent args);
}
