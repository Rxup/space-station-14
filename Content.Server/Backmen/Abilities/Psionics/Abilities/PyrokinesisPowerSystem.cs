using Content.Shared.Actions;
using Content.Server.Atmos.Components;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Popups;
using Content.Shared.Atmos.Components;
using Content.Shared.Backmen.Abilities.Psionics;
using Content.Shared.Backmen.Psionics;
using Content.Shared.Backmen.Psionics.Events;
using Content.Shared.Interaction;
using Content.Shared.Physics;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.Backmen.Abilities.Psionics;

public sealed partial class PyrokinesisPowerSystem : SharedPyrokinesisPowerSystem
{
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private FlammableSystem _flammableSystem = default!;
    [Dependency] private SharedPsionicAbilitiesSystem _psionics = default!;
    [Dependency] private PopupSystem _popupSystem = default!;
    [Dependency] private IGameTiming _gameTiming = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PyrokinesisPowerComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<PyrokinesisPowerComponent, PyrokinesisPowerActionEvent>(OnPowerUsed);
    }

    private readonly EntProtoId ActionPyrokinesis = "ActionPyrokinesis";

    private void OnInit(EntityUid uid, PyrokinesisPowerComponent component, ComponentInit args)
    {
        _actions.AddAction(uid, ref component.PyrokinesisPowerAction, ActionPyrokinesis);

        var actionEnt = _actions.GetAction(component.PyrokinesisPowerAction);
        if (actionEnt is { Comp.UseDelay: {} delay })
            _actions.SetCooldown(component.PyrokinesisPowerAction, delay);

        if (TryComp<PsionicComponent>(uid, out var psionic) && psionic.PsionicAbility == null)
            psionic.PsionicAbility = component.PyrokinesisPowerAction;
    }


    private void OnPowerUsed(Entity<PyrokinesisPowerComponent> ent, ref PyrokinesisPowerActionEvent args)
    {
        if(args.Handled)
            return;

        if (!TryComp<FlammableComponent>(args.Target, out var flammableComponent))
            return;

        flammableComponent.FireStacks += ent.Comp.FireStacks;
        _flammableSystem.Ignite(args.Target, args.Performer, flammableComponent);
        _popupSystem.PopupEntity(Loc.GetString("pyrokinesis-power-used", ("target", args.Target)),
            args.Target,
            Shared.Popups.PopupType.LargeCaution);

        _psionics.LogPowerUsed(args.Performer, "pyrokinesis");

        args.Handled = true;

        if (TryComp<NoosphericZapPowerComponent>(args.Performer, out var powerComponent))
        {
            var actionEnt = _actions.GetAction(powerComponent.NoosphericZapPowerAction);
            _actions.SetCooldown(powerComponent.NoosphericZapPowerAction, actionEnt?.Comp.UseDelay ?? TimeSpan.FromMinutes(1));
        }
    }
}
