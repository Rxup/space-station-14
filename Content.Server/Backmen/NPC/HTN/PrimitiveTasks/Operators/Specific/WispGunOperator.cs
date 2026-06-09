using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Content.Server.Backmen.NPC.HTN;
using Content.Server.NPC;
using Content.Server.NPC.Components;
using Content.Server.NPC.HTN;
using Content.Server.NPC.HTN.PrimitiveTasks;
using Content.Shared.CombatMode;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Robust.Shared.Audio;
using Robust.Shared.Maths;

namespace Content.Server.Backmen.NPC.HTN.PrimitiveTasks.Operators.Specific;

/// <summary>
/// Wisp gun combat via upstream <see cref="NPCRangedCombatComponent"/> and <see cref="NPCCombatSystem"/>.
/// Adds failed-target blacklisting for <see cref="FailedGunTargetCon"/>.
/// </summary>
public sealed partial class WispGunOperator : HTNOperator, IHtnConditionalShutdown
{
    [Dependency] private IEntityManager _entManager = default!;

    [DataField("shutdownState")]
    public HTNPlanState ShutdownState { get; private set; } = HTNPlanState.TaskFinished;

    [DataField("targetKey", required: true)]
    public string TargetKey = default!;

    [DataField("targetState")]
    public MobState TargetState = MobState.Alive;

    [DataField("requireLOS")]
    public bool RequireLOS;

    [DataField("opaqueKey")]
    public bool UseOpaqueForLOSChecks;

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

    public override void Startup(NPCBlackboard blackboard)
    {
        base.Startup(blackboard);

        var ranged = _entManager.EnsureComponent<NPCRangedCombatComponent>(blackboard.GetValue<EntityUid>(NPCBlackboard.Owner));
        ranged.Target = blackboard.GetValue<EntityUid>(TargetKey);
        ranged.UseOpaqueForLOSChecks = UseOpaqueForLOSChecks;

        if (blackboard.TryGetValue<float>(NPCBlackboard.RotateSpeed, out var rotSpeed, _entManager))
            ranged.RotationSpeed = new Angle(rotSpeed);

        if (blackboard.TryGetValue<SoundSpecifier>("SoundTargetInLOS", out var losSound, _entManager))
            ranged.SoundTargetInLOS = losSound;
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
        _entManager.System<SharedCombatModeSystem>().SetInCombatMode(owner, false);
        _entManager.RemoveComponent<NPCRangedCombatComponent>(owner);
        _entManager.RemoveComponent<NPCJukeComponent>(owner);
        blackboard.Remove<EntityUid>(TargetKey);
    }

    public override HTNOperatorStatus Update(NPCBlackboard blackboard, float frameTime)
    {
        base.Update(blackboard, frameTime);
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);

        if (!_entManager.TryGetComponent<NPCRangedCombatComponent>(owner, out var combat) ||
            !blackboard.TryGetValue<EntityUid>(TargetKey, out var target, _entManager))
        {
            return HTNOperatorStatus.Failed;
        }

        combat.Target = target;

        if (_entManager.TryGetComponent<MobStateComponent>(combat.Target, out var mobState) &&
            mobState.CurrentState > TargetState)
        {
            return HTNOperatorStatus.Finished;
        }

        var status = combat.Status switch
        {
            CombatStatus.TargetUnreachable => HTNOperatorStatus.Failed,
            CombatStatus.NotInSight => RequireLOS ? HTNOperatorStatus.Failed : HTNOperatorStatus.Continuing,
            CombatStatus.Normal => HTNOperatorStatus.Continuing,
            _ => HTNOperatorStatus.Failed
        };

        if (status == HTNOperatorStatus.Continuing && ShutdownState == HTNPlanState.PlanFinished)
            status = HTNOperatorStatus.Finished;

        return status;
    }
}
