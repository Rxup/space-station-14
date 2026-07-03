using Content.Shared.Actions;
using Content.Shared.Backmen.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Mobs.Components;
using Robust.Shared.Prototypes;

namespace Content.Shared.Backmen.Abilities.Psionics;

public sealed partial class MassSleepPowerSystem : StatusEffectGrantedPowerSystem<MassSleepPowerComponent, MassSleepPowerActionEvent>
{
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private BackmenDamageModelSystem _damageModel = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedPsionicAbilitiesSystem _psionics = default!;
    [Dependency] private StatusEffectNew.StatusEffectsSystem _effectsSystem = default!;

    private static readonly EntProtoId ActionMassSleep = "ActionMassSleep";

    protected override void EnsurePowerActions(EntityUid uid, MassSleepPowerComponent component)
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

    protected override void RemovePowerActions(EntityUid uid, MassSleepPowerComponent component)
    {
        _actions.RemoveAction(uid, component.MassSleepPowerAction);
    }

    private static readonly ProtoId<DamageContainerPrototype> Biological = "Biological";
    private static readonly EntProtoId StatusEffectForcedSleeping = "StatusEffectForcedSleeping";

    protected override void HandlePowerUse(EntityUid uid, MassSleepPowerComponent component, MassSleepPowerActionEvent args)
    {
        if (args.Handled)
            return;

        var handle = false;
        foreach (var entity in _lookup.GetEntitiesInRange<MobStateComponent>(args.Target, component.Radius))
        {
            if (
#if !DEBUG
                entity.Owner == uid ||
#endif

                _effectsSystem.HasEffectComp<PsionicInsulationComponent>(entity))
                continue;

            if (!_damageModel.MatchesDamageContainerFilter(entity, [Biological.Id]))
                continue;

            var result = _effectsSystem.TryUpdateStatusEffectDuration(entity, StatusEffectForcedSleeping, TimeSpan.FromSeconds(10));
            if (!handle && result)
                handle = true;
        }
        if(handle)
            _psionics.LogPowerUsed(uid, "mass sleep");
        args.Handled = handle;
    }
}

public sealed partial class MassSleepPowerActionEvent : WorldTargetActionEvent;
