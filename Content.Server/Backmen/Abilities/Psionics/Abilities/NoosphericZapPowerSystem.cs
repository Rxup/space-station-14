using Content.Server.Backmen.Psionics;
using Content.Shared.Actions;
using Content.Shared.StatusEffect;
using Content.Server.Stunnable;
using Content.Server.Beam;
using Content.Server.Popups;
using Content.Shared.Backmen.Abilities.Psionics;
using Content.Shared.Backmen.Psionics;
using Content.Shared.Backmen.Psionics.Components;
using Content.Shared.Backmen.Psionics.Events;
using Content.Shared.StatusEffectNew;
using Content.Shared.StatusEffectNew.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.Backmen.Abilities.Psionics;

public sealed partial class NoosphericZapPowerSystem
    : StatusEffectGrantedPowerSystem<NoosphericZapPowerComponent, NoosphericZapPowerActionEvent>
{
    [Dependency] private PopupSystem _popupSystem = default!;
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private SharedPsionicAbilitiesSystem _psionics = default!;
    [Dependency] private StunSystem _stunSystem = default!;
    [Dependency] private Content.Shared.StatusEffect.StatusEffectsSystem _statusEffectsSystem = default!;
    [Dependency] private IGameTiming _gameTiming = default!;
    [Dependency] private BeamSystem _beam = default!;

    public override void Initialize()
    {
        base.Initialize();
        InitializeStatusEffectGrantedPower();
    }

    private readonly EntProtoId ActionNoosphericZap = "ActionNoosphericZap";

    protected override void EnsurePowerActions(EntityUid uid, NoosphericZapPowerComponent component)
    {
        _actions.AddAction(uid, ref component.NoosphericZapPowerAction, ActionNoosphericZap);

        /*var actionEnt = _actions.GetAction(component.NoosphericZapPowerAction);
        if (actionEnt is { Comp.UseDelay: {} delay })
            _actions.SetCooldown(component.NoosphericZapPowerAction, delay);*/

        if (TryComp<PsionicComponent>(uid, out var psionic) && psionic.PsionicAbility == null)
            psionic.PsionicAbility = component.NoosphericZapPowerAction;
    }

    protected override void RemovePowerActions(EntityUid uid, NoosphericZapPowerComponent component)
    {
        _actions.RemoveAction(uid, component.NoosphericZapPowerAction);
    }

    protected override void HandlePowerUse(EntityUid uid, NoosphericZapPowerComponent component, NoosphericZapPowerActionEvent args)
    {
        if (args.Handled)
            return;

        _beam.TryCreateBeam(args.Performer, args.Target, "LightningNoospheric");

        _stunSystem.TryUpdateParalyzeDuration(args.Target, TimeSpan.FromSeconds(5));
        _statusEffectsSystem.TryAddStatusEffect(args.Target, "Stutter", TimeSpan.FromSeconds(10), false, "StutteringAccent");

        _psionics.LogPowerUsed(args.Performer, "noospheric zap");
        args.Handled = true;

        if (TryComp<NoosphericZapPowerComponent>(args.Performer, out var powerComponent)
            && _actions.GetAction(powerComponent.NoosphericZapPowerAction) is {} action)
        {
            _actions.SetCooldown(powerComponent.NoosphericZapPowerAction, action.Comp.UseDelay ?? TimeSpan.FromMinutes(1));
        }
    }
}


