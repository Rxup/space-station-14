using Content.Shared.Actions;
using Content.Server.Atmos.Components;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Popups;
using Content.Shared.Backmen.Abilities.Psionics;
using Content.Shared.Backmen.Psionics;
using Content.Shared.Backmen.Psionics.Events;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.Backmen.Abilities.Psionics;

public sealed class PyrokinesisPowerSystem : EntitySystem
{
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly FlammableSystem _flammableSystem = default!;
    [Dependency] private readonly SharedPsionicAbilitiesSystem _psionics = default!;
    [Dependency] private readonly PopupSystem _popupSystem = default!;
    [Dependency] private readonly IGameTiming _gameTiming = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PyrokinesisPowerComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<PyrokinesisPowerComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<PyrokinesisPowerActionEvent>(OnPowerUsed);
    }

    [ValidatePrototypeId<EntityPrototype>] private const string ActionPyrokinesis = "ActionPyrokinesis";

    private void OnInit(EntityUid uid, PyrokinesisPowerComponent component, ComponentInit args)
    {
        _actions.AddAction(uid, ref component.PyrokinesisPowerAction, ActionPyrokinesis);

        if (_actions.TryGetActionData(component.PyrokinesisPowerAction, out var action) && action?.UseDelay != null)
            _actions.SetCooldown(component.PyrokinesisPowerAction, _gameTiming.CurTime,
                _gameTiming.CurTime + (TimeSpan) action?.UseDelay!);

        if (TryComp<PsionicComponent>(uid, out var psionic) && psionic.PsionicAbility == null)
            psionic.PsionicAbility = component.PyrokinesisPowerAction;
    }

    private void OnShutdown(EntityUid uid, PyrokinesisPowerComponent component, ComponentShutdown args)
    {
        _actions.RemoveAction(uid, component.PyrokinesisPowerAction);
    }

    private void OnPowerUsed(PyrokinesisPowerActionEvent args)
    {
        if (HasComp<PsionicallyInvisibleComponent>(args.Performer))
        {
            _popupSystem.PopupCursor(Loc.GetString("cant-use-in-invisible"),args.Performer);
            return;
        }
        if (HasComp<PsionicInsulationComponent>(args.Target))
            return;
        if (!TryComp<FlammableComponent>(args.Target, out var flammableComponent))
            return;

        flammableComponent.FireStacks += 5;
        _flammableSystem.Ignite(args.Target, args.Performer, flammableComponent);
        _popupSystem.PopupEntity(Loc.GetString("pyrokinesis-power-used", ("target", args.Target)), args.Target,
            Shared.Popups.PopupType.LargeCaution);

        _psionics.LogPowerUsed(args.Performer, "pyrokinesis");
        args.Handled = true;
    }
}
