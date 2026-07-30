using Content.Server.Backmen.Species.Shadowkin.Components;
using Content.Server.Backmen.Species.Shadowkin.Events;
using Content.Shared.Actions;
using Content.Shared.Bed.Sleep;
using Content.Shared.Backmen.Species.Shadowkin.Components;
using Content.Shared.StatusEffectNew;
using Content.Shared.Stunnable;
using Robust.Shared.Prototypes;

namespace Content.Server.Backmen.Species.Shadowkin.Systems;

public sealed partial class ShadowkinRestSystem : EntitySystem
{
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private ShadowkinPowerSystem _power = default!;
    [Dependency] private SleepingSystem _sleeping = default!;
    [Dependency] private StatusEffectsSystem _statusEffects = default!;

    private static readonly EntProtoId ShadowkinRest = "ShadowkinRest";

    public override void Initialize()
    {
        base.Initialize();

        // ActionGrant adds the button on MapInit; we only keep a handle for cooldowns.
        SubscribeLocalEvent<ShadowkinRestPowerComponent, MapInitEvent>(OnMapInit, after: [typeof(ActionGrantSystem)]);
        SubscribeLocalEvent<ShadowkinRestPowerComponent, ComponentShutdown>(OnShutdown);

        SubscribeLocalEvent<ShadowkinRestPowerComponent, ShadowkinRestEvent>(Rest);

        SubscribeLocalEvent<SleepingComponent, RefreshShadowkinPowerModifiersEvent>(OnRest);
        SubscribeLocalEvent<ShadowkinRestPowerComponent, SleepStateChangedEvent>(OnSleepStateChanged);
    }

    private void OnSleepStateChanged(Entity<ShadowkinRestPowerComponent> ent, ref SleepStateChangedEvent args)
    {
        _power.RefreshPowerModifiers(ent);
    }

    private void OnRest(Entity<SleepingComponent> ent, ref RefreshShadowkinPowerModifiersEvent args)
    {
        args.ModifySpeed(1.5f);
    }

    private void OnMapInit(Entity<ShadowkinRestPowerComponent> ent, ref MapInitEvent args)
    {
        LinkOrAddAction(ent);
    }

    private void LinkOrAddAction(Entity<ShadowkinRestPowerComponent> ent)
    {
        if (ent.Comp.ShadowkinRestAction is { Valid: true })
            return;

        if (TryComp<ActionGrantComponent>(ent, out var grant))
        {
            foreach (var action in grant.ActionEntities)
            {
                if (MetaData(action).EntityPrototype?.ID == ShadowkinRest.Id)
                {
                    ent.Comp.ShadowkinRestAction = action;
                    return;
                }
            }
        }

        _actions.AddAction(ent, ref ent.Comp.ShadowkinRestAction, ShadowkinRest);
    }

    private void OnShutdown(EntityUid uid, ShadowkinRestPowerComponent component, ComponentShutdown args)
    {
        _actions.RemoveAction(uid, component.ShadowkinRestAction);
    }

    private void Rest(EntityUid uid, ShadowkinRestPowerComponent component, ShadowkinRestEvent args)
    {
        // Need power to modify power
        if (!HasComp<ShadowkinComponent>(args.Performer) || component.ShadowkinRestAction is null)
            return;

        SleepingComponent? sleepingComponent = null;

        // Resting
        var isSleepingByPower = HasComp<ShadowkinRestPowerUsedComponent>(args.Performer);
        if (!isSleepingByPower)
        {
            if (HasComp<StunnedComponent>(args.Performer))
                return;

            if (!_sleeping.TrySleeping(args.Performer))
                return;

            EnsureComp<ShadowkinRestPowerUsedComponent>(args.Performer);

            // Forced sleep until the shadowkin uses Rest again to wake.
            _statusEffects.TrySetStatusEffectDuration(
                args.Performer,
                SleepingSystem.StatusEffectForcedSleeping);

            if (TryComp(args.Performer, out sleepingComponent) && sleepingComponent.WakeAction is { Valid: true })
                _actions.SetEnabled(sleepingComponent.WakeAction, false);

            _actions.SetCooldown(component.ShadowkinRestAction.Value, TimeSpan.FromSeconds(1));
            args.Handled = true;
        }
        // Waking
        else if (isSleepingByPower && TryComp(args.Performer, out sleepingComponent))
        {
            if (sleepingComponent.WakeAction is { Valid: true })
                _actions.SetEnabled(sleepingComponent.WakeAction, true);

            RemCompDeferred<ShadowkinRestPowerUsedComponent>(args.Performer);
            _statusEffects.TryRemoveStatusEffect(args.Performer, SleepingSystem.StatusEffectForcedSleeping);
            args.Handled = _sleeping.TryWaking((args.Performer, sleepingComponent), true);
            _actions.SetCooldown(component.ShadowkinRestAction.Value, TimeSpan.FromMinutes(1));
        }
    }
}
