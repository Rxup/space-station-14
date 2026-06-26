using Content.Server.Backmen.Vampiric;
using Content.Server.NPC;
using Content.Server.NPC.HTN;
using Content.Server.NPC.HTN.PrimitiveTasks;
using Content.Shared.DoAfter;

namespace Content.Server.Backmen.NPC.HTN.PrimitiveTasks.Operators.Specific;

public sealed partial class BloodSuccOperator : HTNOperator
{
    [Dependency] private IEntityManager _entManager = default!;
    private BloodSuckerSystem _bloodSucker = default!;
    private SharedDoAfterSystem _doAfter = default!;

    [DataField("targetKey", required: true)]
    public string TargetKey = default!;

    public string CurrentDoAfter = "CurrentBloodSuccDoAfter";

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
        _bloodSucker = sysManager.GetEntitySystem<BloodSuckerSystem>();
        _doAfter = sysManager.GetEntitySystem<SharedDoAfterSystem>();
    }

    public override void Startup(NPCBlackboard blackboard)
    {
        blackboard.Remove<ushort>(CurrentDoAfter);
    }

    public override void TaskShutdown(NPCBlackboard blackboard, HTNOperatorStatus status)
    {
        blackboard.Remove<ushort>(CurrentDoAfter);

        if (status == HTNOperatorStatus.Failed &&
            blackboard.TryGetValue<EntityUid>(TargetKey, out var target, _entManager))
        {
            var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);

            if (!_bloodSucker.HasDrinkableCocoonMeal(owner, target, checkRange: false))
                RememberFailedMeal(blackboard, target);
        }
    }

    public override HTNOperatorStatus Update(NPCBlackboard blackboard, float frameTime)
    {
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);

        if (blackboard.TryGetValue<ushort>(CurrentDoAfter, out var doAfterId, _entManager))
        {
            switch (_doAfter.GetStatus(owner, doAfterId, null))
            {
                case DoAfterStatus.Running:
                    return HTNOperatorStatus.Continuing;
                case DoAfterStatus.Finished:
                    if (!_bloodSucker.NeedsBlood(owner))
                        return HTNOperatorStatus.Finished;

                    // One sip succeeded but the stomach still wants blood — drink again, do not blacklist.
                    if (blackboard.TryGetValue<EntityUid>(TargetKey, out var meal, _entManager) &&
                        _bloodSucker.HasDrinkableCocoonMeal(owner, meal, checkRange: false))
                    {
                        blackboard.Remove<ushort>(CurrentDoAfter);
                        return HTNOperatorStatus.Continuing;
                    }

                    return HTNOperatorStatus.Failed;
                default:
                    return HTNOperatorStatus.Failed;
            }
        }

        if (!blackboard.TryGetValue<EntityUid>(TargetKey, out var target, _entManager) ||
            !_entManager.EntityExists(target))
        {
            return HTNOperatorStatus.Failed;
        }

        ushort nextId = 0;
        if (_entManager.TryGetComponent<DoAfterComponent>(owner, out var doAfter))
            nextId = doAfter.NextId;

        if (!_bloodSucker.NPCStartSucc(owner, target))
        {
            if (_bloodSucker.HasDrinkableCocoonMeal(owner, target, checkRange: false))
                return HTNOperatorStatus.Continuing;

            return HTNOperatorStatus.Failed;
        }

        if (_entManager.TryGetComponent<DoAfterComponent>(owner, out doAfter) && nextId != doAfter.NextId)
        {
            blackboard.SetValue(CurrentDoAfter, nextId);
            return HTNOperatorStatus.Continuing;
        }

        return HTNOperatorStatus.Failed;
    }

    private void RememberFailedMeal(NPCBlackboard blackboard, EntityUid target)
    {
        if (!blackboard.TryGetValue<HashSet<EntityUid>>(BloodSuckerSystem.FailedCocoonMealsKey, out var failed, _entManager))
        {
            failed = new HashSet<EntityUid>();
            blackboard.SetValue(BloodSuckerSystem.FailedCocoonMealsKey, failed);
        }

        failed.Add(target);
    }
}

