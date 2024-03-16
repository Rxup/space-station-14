using Content.Shared.Actions;
using Content.Shared.Bed.Sleep;
using Content.Shared.Damage;
using Content.Shared.Mobs.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared.Backmen.Abilities.Psionics;

public sealed class MassSleepPowerSystem : EntitySystem
{
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedPsionicAbilitiesSystem _psionics = default!;
    [Dependency] private readonly IGameTiming _gameTiming = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MassSleepPowerComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<MassSleepPowerComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<MassSleepPowerComponent, MassSleepPowerActionEvent>(OnPowerUsed);
    }

    [ValidatePrototypeId<EntityPrototype>] private const string ActionMassSleep = "ActionMassSleep";

    private void OnInit(EntityUid uid, MassSleepPowerComponent component, ComponentInit args)
    {
        _actions.AddAction(uid, ref component.MassSleepPowerAction, ActionMassSleep);

        if (_actions.TryGetActionData(component.MassSleepPowerAction, out var action) && action?.UseDelay != null)
            _actions.SetCooldown(component.MassSleepPowerAction, _gameTiming.CurTime,
                _gameTiming.CurTime + (TimeSpan) action?.UseDelay!);

        if (TryComp<PsionicComponent>(uid, out var psionic) && psionic.PsionicAbility == null)
            psionic.PsionicAbility = component.MassSleepPowerAction;
    }

    private void OnShutdown(EntityUid uid, MassSleepPowerComponent component, ComponentShutdown args)
    {
        _actions.RemoveAction(uid, component.MassSleepPowerAction);
    }

    private void OnPowerUsed(EntityUid uid, MassSleepPowerComponent component, MassSleepPowerActionEvent args)
    {
        foreach (var entity in _lookup.GetEntitiesInRange(args.Target, component.Radius))
        {
            if (HasComp<MobStateComponent>(entity) && entity != uid && !HasComp<PsionicInsulationComponent>(entity))
            {
                if (TryComp<DamageableComponent>(entity, out var damageable) && damageable.DamageContainerID == "Biological")
                    EnsureComp<SleepingComponent>(entity);
            }
        }
        _psionics.LogPowerUsed(uid, "mass sleep");
        args.Handled = true;
    }
}

public sealed partial class MassSleepPowerActionEvent : WorldTargetActionEvent {}
