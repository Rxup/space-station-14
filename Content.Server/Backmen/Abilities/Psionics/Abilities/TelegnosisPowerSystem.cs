using Content.Shared.Actions;
using Content.Shared.Backmen.Abilities.Psionics;
using Content.Shared.Backmen.Psionics.Events;
using Content.Shared.Mind.Components;
using Content.Shared.StatusEffect;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.Backmen.Abilities.Psionics;

public sealed class TelegnosisPowerSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly StatusEffectsSystem _statusEffects = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly MindSwapPowerSystem _mindSwap = default!;
    [Dependency] private readonly SharedPsionicAbilitiesSystem _psionics = default!;
    [Dependency] private readonly IGameTiming _gameTiming = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<TelegnosisPowerComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<TelegnosisPowerComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<TelegnosisPowerComponent, TelegnosisPowerActionEvent>(OnPowerUsed);
        SubscribeLocalEvent<TelegnosticProjectionComponent, MindRemovedMessage>(OnMindRemoved);
    }

    [ValidatePrototypeId<EntityPrototype>] private const string ActionTelegnosis = "ActionTelegnosis";

    private void OnInit(EntityUid uid, TelegnosisPowerComponent component, ComponentInit args)
    {
        _actions.AddAction(uid, ref component.TelegnosisPowerAction, ActionTelegnosis);

        var action = _actions.GetActionData(component.TelegnosisPowerAction);

        if (action?.UseDelay != null)
            _actions.SetCooldown(component.TelegnosisPowerAction, _gameTiming.CurTime,
                _gameTiming.CurTime + (TimeSpan)  action?.UseDelay!);

        if (TryComp<PsionicComponent>(uid, out var psionic) && psionic.PsionicAbility == null)
            psionic.PsionicAbility = component.TelegnosisPowerAction;
    }

    private void OnShutdown(EntityUid uid, TelegnosisPowerComponent component, ComponentShutdown args)
    {
        _actions.RemoveAction(uid, ActionTelegnosis);
    }

    private void OnPowerUsed(EntityUid uid, TelegnosisPowerComponent component, TelegnosisPowerActionEvent args)
    {
        var projection = Spawn(component.Prototype, Transform(uid).Coordinates);
        Transform(projection).AttachToGridOrMap();
        _mindSwap.Swap(uid, projection);

        _psionics.LogPowerUsed(uid, "telegnosis");
        args.Handled = true;
    }
    private void OnMindRemoved(EntityUid uid, TelegnosticProjectionComponent component, MindRemovedMessage args)
    {
        QueueDel(uid);
    }
}


