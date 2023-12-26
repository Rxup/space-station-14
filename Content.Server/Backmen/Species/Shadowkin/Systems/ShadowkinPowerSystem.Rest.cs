using Content.Server.Backmen.Species.Shadowkin.Components;
using Content.Server.Backmen.Species.Shadowkin.Events;
using Content.Shared.Actions;
using Content.Shared.Bed.Sleep;
using Content.Shared.Cuffs.Components;
using Content.Shared.Backmen.Species.Shadowkin.Components;
using Robust.Shared.Prototypes;

namespace Content.Server.Backmen.Species.Shadowkin.Systems;

public sealed class ShadowkinRestSystem : EntitySystem
{
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly ShadowkinPowerSystem _power = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ShadowkinRestPowerComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<ShadowkinRestPowerComponent, ComponentShutdown>(OnShutdown);

        SubscribeLocalEvent<ShadowkinRestPowerComponent, ShadowkinRestEvent>(Rest);
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


        // Now doing what you weren't before
        component.IsResting = !component.IsResting;

        // Resting
        if (component.IsResting)
        {
            // Sleepy time
            EnsureComp<ForcedSleepingComponent>(args.Performer);
            // No waking up normally (it would do nothing)
            //_actions.RemoveAction(args.Performer, new InstantAction(_prototype.Index<InstantActionPrototype>("Wake")));
            if(TryComp<SleepingComponent>(args.Performer, out var sleepingComponent))
                _actions.RemoveAction(args.Performer, sleepingComponent.WakeAction);
            _power.TryAddMultiplier(args.Performer, 1.5f);
            // No action cooldown
            args.Handled = false;
        }
        // Waking
        else
        {
            // Wake up
            RemCompDeferred<ForcedSleepingComponent>(args.Performer);
            RemCompDeferred<SleepingComponent>(args.Performer);
            _power.TryAddMultiplier(args.Performer, -1.5f);
            // Action cooldown
            args.Handled = true;
        }
    }
}
