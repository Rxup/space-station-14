using Content.Shared.Actions;
using Content.Shared.Bed.Sleep;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Mobs.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared.Backmen.Abilities.Psionics;

public sealed class MassSleepPowerSystem : EntitySystem
{
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedPsionicAbilitiesSystem _psionics = default!;
    [Dependency] private readonly StatusEffectNew.StatusEffectsSystem _effectsSystem = default!;
    private EntityQuery<PsionicInsulationComponent> _qPsionicInsulation;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MassSleepPowerComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<MassSleepPowerComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<MassSleepPowerComponent, MassSleepPowerActionEvent>(OnPowerUsed);

        _qPsionicInsulation = GetEntityQuery<PsionicInsulationComponent>();
    }

    private readonly EntProtoId ActionMassSleep = "ActionMassSleep";

    private void OnInit(EntityUid uid, MassSleepPowerComponent component, ComponentInit args)
    {
        _actions.AddAction(uid, ref component.MassSleepPowerAction, ActionMassSleep);

#if !DEBUG
        var actionEnt = _actions.GetAction(component.MassSleepPowerAction);
        if (actionEnt is { Comp.UseDelay: {} delay }) {
            _actions.SetCooldown(component.MassSleepPowerAction, delay);
        }
#endif


        if (TryComp<PsionicComponent>(uid, out var psionic) && psionic.PsionicAbility == null)
            psionic.PsionicAbility = component.MassSleepPowerAction;
    }

    private void OnShutdown(EntityUid uid, MassSleepPowerComponent component, ComponentShutdown args)
    {
        _actions.RemoveAction(uid, component.MassSleepPowerAction);
    }

    private static readonly ProtoId<DamageContainerPrototype> Biological = "Biological";

    private void OnPowerUsed(EntityUid uid, MassSleepPowerComponent component, MassSleepPowerActionEvent args)
    {
        var handle = false;
        foreach (var entity in _lookup.GetEntitiesInRange<MobStateComponent>(args.Target, component.Radius))
        {
            if (
#if !DEBUG
                entity.Owner == uid ||
#endif

                _qPsionicInsulation.HasComp(entity))
                continue;

            if (!TryComp<DamageableComponent>(entity, out var damageable) || damageable.DamageContainerID != Biological)
                continue;

            var result = _effectsSystem.TryUpdateStatusEffectDuration(entity, "StatusEffectForcedSleeping", TimeSpan.FromSeconds(10));
            if (!handle && result)
                handle = true;
        }
        if(handle)
            _psionics.LogPowerUsed(uid, "mass sleep");
        args.Handled = handle;
    }
}

public sealed partial class MassSleepPowerActionEvent : WorldTargetActionEvent {}
