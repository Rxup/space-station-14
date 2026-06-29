using System.Threading;
using System.Threading.Tasks;
using Content.Shared.Backmen.Flesh;
using Content.Server.NPC;
using Content.Server.NPC.Components;
using Content.Server.NPC.HTN;
using Content.Server.NPC.HTN.PrimitiveTasks;
using Content.Shared.CombatMode;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;

namespace Content.Server.Backmen.NPC.HTN.PrimitiveTasks.Operators.Specific;

/// <summary>
/// Melee combat that finishes successfully when a pounce is queued, so the next operator can attach to the victim.
/// </summary>
public sealed partial class FleshWormMeleeOperator : HTNOperator, IHtnConditionalShutdown
{
    [Dependency] private IEntityManager _entManager = default!;

    [DataField("shutdownState")]
    public HTNPlanState ShutdownState { get; private set; } = HTNPlanState.TaskFinished;

    [DataField("targetKey", required: true)]
    public string TargetKey = default!;

    [DataField("targetState")]
    public MobState TargetState = MobState.Alive;

    public override void Startup(NPCBlackboard blackboard)
    {
        base.Startup(blackboard);
        var melee = _entManager.EnsureComponent<NPCMeleeCombatComponent>(blackboard.GetValue<EntityUid>(NPCBlackboard.Owner));
        melee.MissChance = blackboard.GetValueOrDefault<float>(NPCBlackboard.MeleeMissChance, _entManager);
        melee.Target = blackboard.GetValue<EntityUid>(TargetKey);
    }

    public override async Task<(bool Valid, Dictionary<string, object>? Effects)> Plan(NPCBlackboard blackboard,
        CancellationToken cancelToken)
    {
        if (!blackboard.TryGetValue<EntityUid>(TargetKey, out var target, _entManager))
            return (false, null);

        if (_entManager.TryGetComponent<MobStateComponent>(target, out var mobState) &&
            mobState.CurrentState > TargetState)
        {
            return (false, null);
        }

        return (true, null);
    }

    public void ConditionalShutdown(NPCBlackboard blackboard)
    {
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);
        _entManager.System<SharedCombatModeSystem>().SetInCombatMode(owner, false);
        _entManager.RemoveComponent<NPCMeleeCombatComponent>(owner);
        blackboard.Remove<EntityUid>(TargetKey);
    }

    public override void TaskShutdown(NPCBlackboard blackboard, HTNOperatorStatus status)
    {
        base.TaskShutdown(blackboard, status);
        ConditionalShutdown(blackboard);
    }

    public override void PlanShutdown(NPCBlackboard blackboard)
    {
        base.PlanShutdown(blackboard);
        ConditionalShutdown(blackboard);
    }

    public override HTNOperatorStatus Update(NPCBlackboard blackboard, float frameTime)
    {
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);

        if (_entManager.TryGetComponent<FleshWormComponent>(owner, out var worm)
            && worm.PendingPounceTarget.Valid)
        {
            return HTNOperatorStatus.Finished;
        }

        HTNOperatorStatus status;

        if (_entManager.TryGetComponent<NPCMeleeCombatComponent>(owner, out var combat) &&
            blackboard.TryGetValue<EntityUid>(TargetKey, out var target, _entManager) &&
            target != EntityUid.Invalid)
        {
            combat.Target = target;

            if (_entManager.TryGetComponent<MobStateComponent>(target, out var mobState) &&
                mobState.CurrentState > TargetState)
            {
                status = HTNOperatorStatus.Finished;
            }
            else
            {
                status = combat.Status switch
                {
                    CombatStatus.TargetOutOfRange or CombatStatus.Normal => HTNOperatorStatus.Continuing,
                    _ => HTNOperatorStatus.Failed,
                };
            }
        }
        else
        {
            status = HTNOperatorStatus.Failed;
        }

        if (status == HTNOperatorStatus.Continuing && ShutdownState == HTNPlanState.PlanFinished)
            status = HTNOperatorStatus.Finished;

        return status;
    }
}
