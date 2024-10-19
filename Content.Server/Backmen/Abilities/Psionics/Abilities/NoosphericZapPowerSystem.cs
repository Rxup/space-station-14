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
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.Backmen.Abilities.Psionics;

public sealed class NoosphericZapPowerSystem : SharedNoosphericZapPowerSystem
{
    [Dependency] private readonly PopupSystem _popupSystem = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedPsionicAbilitiesSystem _psionics = default!;
    [Dependency] private readonly StunSystem _stunSystem = default!;
    [Dependency] private readonly StatusEffectsSystem _statusEffectsSystem = default!;
    [Dependency] private readonly IGameTiming _gameTiming = default!;
    [Dependency] private readonly BeamSystem _beam = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<NoosphericZapPowerComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<NoosphericZapPowerComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<NoosphericZapPowerActionEvent>(OnPowerUsed);
    }

    [ValidatePrototypeId<EntityPrototype>] private const string ActionNoosphericZap = "ActionNoosphericZap";

    private void OnInit(EntityUid uid, NoosphericZapPowerComponent component, ComponentInit args)
    {
        _actions.AddAction(uid, ref component.NoosphericZapPowerAction, ActionNoosphericZap);

        if (_actions.TryGetActionData(component.NoosphericZapPowerAction, out var action) && action?.UseDelay != null)
            _actions.SetCooldown(component.NoosphericZapPowerAction, _gameTiming.CurTime,
                _gameTiming.CurTime + (TimeSpan)  action?.UseDelay!);

        if (TryComp<PsionicComponent>(uid, out var psionic) && psionic.PsionicAbility == null)
            psionic.PsionicAbility = component.NoosphericZapPowerAction;
    }

    private void OnShutdown(EntityUid uid, NoosphericZapPowerComponent component, ComponentShutdown args)
    {
        _actions.RemoveAction(uid, component.NoosphericZapPowerAction);
    }

    private void OnPowerUsed(NoosphericZapPowerActionEvent args)
    {
        if(args.Handled)
            return;

        _beam.TryCreateBeam(args.Performer, args.Target, "LightningNoospheric");

        _stunSystem.TryParalyze(args.Target, TimeSpan.FromSeconds(5), false);
        _statusEffectsSystem.TryAddStatusEffect(args.Target, "Stutter", TimeSpan.FromSeconds(10), false, "StutteringAccent");

        _psionics.LogPowerUsed(args.Performer, "noospheric zap");
        args.Handled = true;

        if (TryComp<PyrokinesisPowerComponent>(args.Performer, out var powerComponent)
            && _actions.TryGetActionData(powerComponent.PyrokinesisPowerAction, out var action))
        {
            _actions.SetCooldown(powerComponent.PyrokinesisPowerAction, action.UseDelay ?? TimeSpan.FromMinutes(1));
        }
    }
}


