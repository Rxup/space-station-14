using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Content.Server.Backmen.NPC.HTN;
using Content.Server.Interaction;
using Content.Server.NPC;
using Content.Server.NPC.Components;
using Content.Server.NPC.HTN;
using Content.Server.NPC.HTN.PrimitiveTasks;
using Content.Server.NPC.Systems;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Backmen.Abilities.Psionics;
using Content.Shared.CombatMode;
using Content.Shared.Interaction;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Physics;
using Robust.Shared.Timing;

namespace Content.Server.Backmen.NPC.HTN.PrimitiveTasks.Operators.Specific;

/// <summary>
/// Fires NoosphericZap from HTN without a dedicated combat EntitySystem.
/// </summary>
public sealed partial class ZapOperator : HTNOperator, IHtnConditionalShutdown
{
    private const string LosAccumulatorKey = "ZapLosAccumulator";
    private const string TargetInLosKey = "ZapTargetInLOS";

    [Dependency] private IEntityManager _entManager = default!;
    private InteractionSystem _interaction = default!;
    private SharedActionsSystem _actions = default!;
    private SharedCombatModeSystem _combat = default!;
    private SharedTransformSystem _transform = default!;
    private IGameTiming _timing = default!;

    [DataField("shutdownState")]
    public HTNPlanState ShutdownState { get; private set; } = HTNPlanState.TaskFinished;

    [DataField("targetKey", required: true)]
    public string TargetKey = default!;

    [DataField("targetState")]
    public MobState TargetState = MobState.Alive;

    [DataField("opaqueKey")]
    public bool UseOpaqueForLOSChecks;

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
        _interaction = sysManager.GetEntitySystem<InteractionSystem>();
        _actions = sysManager.GetEntitySystem<SharedActionsSystem>();
        _combat = sysManager.GetEntitySystem<SharedCombatModeSystem>();
        _transform = sysManager.GetEntitySystem<SharedTransformSystem>();
        _timing = IoCManager.Resolve<IGameTiming>();
    }

    public override async Task<(bool Valid, Dictionary<string, object>? Effects)> Plan(NPCBlackboard blackboard,
        CancellationToken cancelToken)
    {
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);

        if (!TryGetZapAction(owner, out var action, out var skill) ||
            !blackboard.TryGetValue<EntityUid>(TargetKey, out var target, _entManager) ||
            !_actions.ValidateEntityTarget(owner, target, (action, skill)))
        {
            return (false, null);
        }

        if (_entManager.TryGetComponent<MobStateComponent>(target, out var mobState) &&
            mobState.CurrentState > TargetState)
        {
            return (false, null);
        }

        if (_actions.IsCooldownActive(action, _timing.CurTime))
            return (false, null);

        return (true, null);
    }

    public override void Startup(NPCBlackboard blackboard)
    {
        base.Startup(blackboard);

        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);

        if (_entManager.TryGetComponent<CombatModeComponent>(owner, out var combatMode))
            _combat.SetInCombatMode(owner, true, combatMode);

        blackboard.SetValue(LosAccumulatorKey, 0f);
        blackboard.SetValue(TargetInLosKey, false);
    }

    public override void TaskShutdown(NPCBlackboard blackboard, HTNOperatorStatus status)
    {
        if (status == HTNOperatorStatus.Failed &&
            blackboard.TryGetValue<EntityUid>(TargetKey, out var target, _entManager))
        {
            if (!blackboard.TryGetValue<HashSet<EntityUid>>(NPCRangedBlackboard.FailedGunTargetsKey, out var failed, _entManager))
            {
                failed = new HashSet<EntityUid>();
                blackboard.SetValue(NPCRangedBlackboard.FailedGunTargetsKey, failed);
            }

            failed.Add(target);
        }
    }

    public void ConditionalShutdown(NPCBlackboard blackboard)
    {
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);

        if (_entManager.TryGetComponent<CombatModeComponent>(owner, out var combatMode))
            _combat.SetInCombatMode(owner, false, combatMode);
        _entManager.RemoveComponent<NPCJukeComponent>(owner);
        blackboard.Remove<EntityUid>(TargetKey);
        blackboard.Remove<float>(LosAccumulatorKey);
        blackboard.Remove<bool>(TargetInLosKey);
    }

    public override HTNOperatorStatus Update(NPCBlackboard blackboard, float frameTime)
    {
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);

        if (!TryGetZapAction(owner, out var action, out var skill) ||
            !blackboard.TryGetValue<EntityUid>(TargetKey, out var target, _entManager) ||
            !_actions.ValidateEntityTarget(owner, target, (action, skill)))
        {
            return HTNOperatorStatus.Failed;
        }

        if (_entManager.TryGetComponent<MobStateComponent>(target, out var mobState) &&
            mobState.CurrentState > TargetState)
        {
            return HTNOperatorStatus.Finished;
        }

        if (_actions.IsCooldownActive(action, _timing.CurTime))
            return HTNOperatorStatus.Finished;

        if (!_entManager.TryGetComponent<TransformComponent>(owner, out var xform) ||
            !_entManager.TryGetComponent<TransformComponent>(target, out var targetXform))
        {
            return HTNOperatorStatus.Failed;
        }

        if (targetXform.MapID != xform.MapID)
            return HTNOperatorStatus.Failed;

        if (_entManager.TryGetComponent<NPCSteeringComponent>(owner, out var steering) &&
            steering.Status == SteeringStatus.NoPath)
        {
            return HTNOperatorStatus.Failed;
        }

        var losAccumulator = blackboard.GetValueOrDefault<float>(LosAccumulatorKey, _entManager) - frameTime;
        blackboard.SetValue(LosAccumulatorKey, losAccumulator);

        var inLos = blackboard.GetValueOrDefault<bool>(TargetInLosKey, _entManager);

        if (losAccumulator < 0f)
        {
            losAccumulator = NPCCombatSystem.UnoccludedCooldown;
            blackboard.SetValue(LosAccumulatorKey, losAccumulator);

            var worldPos = _transform.GetWorldPosition(xform);
            var targetPos = _transform.GetWorldPosition(targetXform);
            var distance = (targetPos - worldPos).Length();
            var collisionGroup = UseOpaqueForLOSChecks
                ? CollisionGroup.Opaque
                : (CollisionGroup.Impassable | CollisionGroup.InteractImpassable);
            inLos = _interaction.InRangeUnobstructed(owner, target, distance + 0.1f, collisionGroup);
            blackboard.SetValue(TargetInLosKey, inLos);
        }

        if (!inLos)
        {
            if (_entManager.TryGetComponent<NPCSteeringComponent>(owner, out steering))
                steering.ForceMove = true;

            return HTNOperatorStatus.Failed;
        }

        TryPerformZap(owner, target, action, skill);

        if (_actions.IsCooldownActive(action, _timing.CurTime))
            return HTNOperatorStatus.Finished;

        return HTNOperatorStatus.Continuing;
    }

    private void TryPerformZap(
        EntityUid owner,
        EntityUid target,
        Entity<ActionComponent> action,
        EntityTargetActionComponent skill)
    {
        if (_actions.IsCooldownActive(action, _timing.CurTime))
            return;

        if (!_actions.ValidateEntityTarget(owner, target, (action, skill)))
            return;

        var ev = (EntityTargetActionEvent?) _actions.GetEvent(action);
        if (ev == null)
            return;

        ev.Performer = owner;
        ev.Target = target;
        _actions.PerformAction(owner, action, ev);
    }

    private bool TryGetZapAction(EntityUid uid, out Entity<ActionComponent> action, out EntityTargetActionComponent skill)
    {
        action = default;
        skill = default!;

        if (!_entManager.TryGetComponent<NoosphericZapPowerComponent>(uid, out var zap) ||
            zap.NoosphericZapPowerAction is not {} actionUid ||
            _actions.GetAction(actionUid) is not {} actionEnt ||
            !_entManager.TryGetComponent(actionEnt, out EntityTargetActionComponent? skillComp) ||
            skillComp.Event is null)
        {
            return false;
        }

        action = actionEnt;
        skill = skillComp;
        return true;
    }
}
