using Content.Server.Backmen.Species.Shadowkin.Components;
using Content.Server.Backmen.Species.Shadowkin.Events;
using Content.Shared.Actions;
using Content.Shared.Bed.Sleep;
using Content.Shared.Cuffs.Components;
using Content.Shared.Backmen.Species.Shadowkin.Components;
using Content.Shared.Stunnable;
using Robust.Shared.Prototypes;

namespace Content.Server.Backmen.Species.Shadowkin.Systems;

public sealed class ShadowkinRestSystem : EntitySystem
{
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly ShadowkinPowerSystem _power = default!;
    [Dependency] private readonly SleepingSystem _sleeping = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ShadowkinRestPowerComponent, ComponentInit>(OnInit);
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

    [ValidatePrototypeId<EntityPrototype>] private const string ShadowkinRest = "ShadowkinRest";
    private void OnInit(Entity<ShadowkinRestPowerComponent> ent, ref ComponentInit args)
    {
        _actions.AddAction(ent, ref ent.Comp.ShadowkinRestAction, ShadowkinRest);
    }

    private void OnShutdown(EntityUid uid, ShadowkinRestPowerComponent component, ComponentShutdown args)
    {
        _actions.RemoveAction(uid, component.ShadowkinRestAction);
    }

    private void Rest(EntityUid uid, ShadowkinRestPowerComponent component, ShadowkinRestEvent args)
    {
        // Need power to modify power
        if (!HasComp<ShadowkinComponent>(args.Performer))
            return;

        // Rest is a funny ability, keep it :)
        // // Don't activate abilities if handcuffed
        // if (_entity.HasComponent<HandcuffComponent>(args.Performer))
        //     return;

        // Resting
        if (!TryComp<SleepingComponent>(args.Performer, out var sleepingComponent))
        {
            if (HasComp<StunnedComponent>(args.Performer))
                return;

            if(!_sleeping.TrySleeping(args.Performer))
                return;
            // Sleepy time
            EnsureComp<ForcedSleepingComponent>(args.Performer);
            // No waking up normally (it would do nothing)
            //_actions.RemoveAction(args.Performer, new InstantAction(_prototype.Index<InstantActionPrototype>("Wake")));
            if (TryComp(args.Performer, out sleepingComponent) && sleepingComponent.WakeAction is { Valid: true })
                _actions.RemoveAction(args.Performer, sleepingComponent.WakeAction);

            // No action cooldown
            args.Handled = false;
        }
        // Waking
        else
        {
            // Wake up
            // Action cooldown
            args.Handled = _sleeping.TryWaking((args.Performer,sleepingComponent), true);
        }
    }
}
