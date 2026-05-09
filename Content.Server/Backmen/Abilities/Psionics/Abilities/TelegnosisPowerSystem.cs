using Content.Server.Mind;
using Content.Shared.Actions;
using Content.Shared.Backmen.Abilities.Psionics;
using Content.Shared.Backmen.Psionics.Events;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs;
using Content.Shared.StatusEffectNew;
using Content.Shared.StatusEffectNew.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Server.Backmen.Abilities.Psionics;

public sealed partial class TelegnosisPowerSystem : StatusEffectGrantedPowerSystem<TelegnosisPowerComponent, TelegnosisPowerActionEvent>
{
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private MindSystem _mindSystem = default!;
    [Dependency] private SharedPsionicAbilitiesSystem _psionics = default!;
    [Dependency] private TransformSystem _transformSystem = default!;
    [Dependency] private MindSwapPowerSystem _mindSwapPowerSystem = default!;

    public override void Initialize()
    {
        base.Initialize();
        InitializeStatusEffectGrantedPower();
        SubscribeLocalEvent<TelegnosisPowerComponent, MobStateChangedEvent>(OnMobStateChanged);

        SubscribeLocalEvent<TelegnosticProjectionComponent, TelegnosisPowerReturnActionEvent>(OnPowerReturnUsed);
        SubscribeLocalEvent<TelegnosticProjectionComponent, MindRemovedMessage>(OnMindRemoved);
    }

    private readonly EntProtoId ActionTelegnosis = "ActionTelegnosis";

    protected override void EnsurePowerActions(EntityUid uid, TelegnosisPowerComponent component)
    {
        _actions.AddAction(uid, ref component.TelegnosisPowerAction, ActionTelegnosis);

        if (TryComp<PsionicComponent>(uid, out var psionic) && psionic.PsionicAbility == null)
            psionic.PsionicAbility = component.TelegnosisPowerAction;
    }

    protected override void RemovePowerActions(EntityUid uid, TelegnosisPowerComponent component)
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

    protected override void HandlePowerUse(EntityUid uid, TelegnosisPowerComponent component, TelegnosisPowerActionEvent args)
    {
        if (args.Handled)
            return;

        if (!_mindSystem.TryGetMind(args.Performer, out var mindId, out var mind))
            return;

        var projection = Spawn(component.Prototype, Transform(args.Performer).Coordinates);
        _transformSystem.AttachToGridOrMap(projection);

        _mindSystem.Visit(mindId, projection, mind);
        //_actions.GrantActions(projection, new []{ component.TelegnosisPowerAction!.Value }, uid);

        component.TelegnosisProjection = projection;

        _psionics.LogPowerUsed(args.Performer, "telegnosis");
        args.Handled = true;
    }
    private void OnMindRemoved(EntityUid uid, TelegnosticProjectionComponent component, MindRemovedMessage args)
    {
        QueueDel(uid);
    }
}


