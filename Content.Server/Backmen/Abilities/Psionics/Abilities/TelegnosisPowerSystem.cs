using Content.Server.Mind;
using Content.Shared.Actions;
using Content.Shared.Backmen.Abilities.Psionics;
using Content.Shared.Backmen.Psionics.Events;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs;
using Robust.Server.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Server.Backmen.Abilities.Psionics;

public sealed class TelegnosisPowerSystem : EntitySystem
{
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly MindSystem _mindSystem = default!;
    [Dependency] private readonly SharedPsionicAbilitiesSystem _psionics = default!;
    [Dependency] private readonly TransformSystem _transformSystem = default!;
    [Dependency] private readonly MindSwapPowerSystem _mindSwapPowerSystem = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<TelegnosisPowerComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<TelegnosisPowerComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<TelegnosisPowerComponent, TelegnosisPowerActionEvent>(OnPowerUsed);
        SubscribeLocalEvent<TelegnosisPowerComponent, MobStateChangedEvent>(OnMobStateChanged);


        SubscribeLocalEvent<TelegnosticProjectionComponent, ComponentStartup>(OnProjectionInit);
        SubscribeLocalEvent<TelegnosticProjectionComponent, ComponentShutdown>(OnProjectionShutdown);
        SubscribeLocalEvent<TelegnosticProjectionComponent, TelegnosisPowerReturnActionEvent>(OnPowerReturnUsed);



        SubscribeLocalEvent<TelegnosticProjectionComponent, MindRemovedMessage>(OnMindRemoved);
    }

    private void OnProjectionShutdown(EntityUid uid, TelegnosticProjectionComponent component, ComponentShutdown args)
    {
        _actions.RemoveAction(uid, component.TelegnosisPowerAction);
    }

    private void OnProjectionInit(EntityUid uid, TelegnosticProjectionComponent component, ComponentStartup args)
    {
        _actions.AddAction(uid, ref component.TelegnosisPowerAction, ActionTelegnosisReturn);
    }

    [ValidatePrototypeId<EntityPrototype>] private const string ActionTelegnosis = "ActionTelegnosis";
    [ValidatePrototypeId<EntityPrototype>] private const string ActionTelegnosisReturn = "ActionTelegnosisReturn";

    private void OnInit(EntityUid uid, TelegnosisPowerComponent component, ComponentInit args)
    {
        _actions.AddAction(uid, ref component.TelegnosisPowerAction, ActionTelegnosis);

        if (TryComp<PsionicComponent>(uid, out var psionic) && psionic.PsionicAbility == null)
            psionic.PsionicAbility = component.TelegnosisPowerAction;
    }

    private void OnShutdown(EntityUid uid, TelegnosisPowerComponent component, ComponentShutdown args)
    {
        _actions.RemoveAction(uid, component.TelegnosisPowerAction);
    }

    private void OnMobStateChanged(EntityUid uid, TelegnosisPowerComponent component, MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead)
            return;
        if (component.TelegnosisProjection == null || TerminatingOrDeleted(component.TelegnosisProjection.Value))
            return;
        if (!_mindSystem.TryGetMind(uid, out var mindId, out var mind))
            return;

        _mindSwapPowerSystem.GetTrapped(component.TelegnosisProjection.Value);
        _mindSystem.UnVisit(mindId, mind);
        _mindSystem.TransferTo(mindId, component.TelegnosisProjection, true, false, mind);
    }

    private void OnPowerReturnUsed(EntityUid uid, TelegnosticProjectionComponent component, TelegnosisPowerReturnActionEvent args)
    {
        if (
            !TryComp<VisitingMindComponent>(args.Performer, out var mindId) ||
            mindId!.MindId == null ||
            !TryComp<MindComponent>(mindId.MindId.Value, out var mind)
            )
            return;

        _mindSystem.UnVisit(mindId.MindId.Value, mind);
        QueueDel(args.Performer);
        _psionics.LogPowerUsed(uid, "telegnosis");
        args.Handled = true;
    }

    private void OnPowerUsed(EntityUid uid, TelegnosisPowerComponent component, TelegnosisPowerActionEvent args)
    {
        if (!_mindSystem.TryGetMind(args.Performer, out var mindId, out var mind))
            return;

        var projection = Spawn(component.Prototype, Transform(args.Performer).Coordinates);
        _transformSystem.AttachToGridOrMap(projection);

        _mindSystem.Visit(mindId, projection, mind);
        //_actions.GrantActions(projection, new []{ component.TelegnosisPowerAction!.Value }, uid);

        component.TelegnosisProjection = projection;

        _psionics.LogPowerUsed(uid, "telegnosis");
        args.Handled = true;
    }
    private void OnMindRemoved(EntityUid uid, TelegnosticProjectionComponent component, MindRemovedMessage args)
    {
        QueueDel(uid);
    }
}


