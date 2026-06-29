using Content.Server.Backmen.Vampiric;
using Content.Server.NPC;
using Content.Server.NPC.HTN;
using Content.Server.NPC.HTN.PrimitiveTasks;

namespace Content.Server.Backmen.NPC.HTN.PrimitiveTasks.Operators.Specific;

public sealed partial class DrinkBloodPuddleOperator : HTNOperator
{
    [Dependency] private IEntityManager _entManager = default!;
    private BloodSuckerSystem _bloodSucker = default!;

    [DataField("targetKey", required: true)]
    public string TargetKey = default!;

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
        _bloodSucker = sysManager.GetEntitySystem<BloodSuckerSystem>();
    }

    public override void TaskShutdown(NPCBlackboard blackboard, HTNOperatorStatus status)
    {
        if (status != HTNOperatorStatus.Failed ||
            !blackboard.TryGetValue<EntityUid>(TargetKey, out var target, _entManager))
        {
            return;
        }

        RememberFailedPuddle(blackboard, target);
    }

    public override HTNOperatorStatus Update(NPCBlackboard blackboard, float frameTime)
    {
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);

        if (!blackboard.TryGetValue<EntityUid>(TargetKey, out var target, _entManager) ||
            !_entManager.EntityExists(target))
        {
            return HTNOperatorStatus.Failed;
        }

        if (!_bloodSucker.TryDrinkBloodPuddle(owner, target))
            return HTNOperatorStatus.Failed;

        if (blackboard.TryGetValue<HashSet<EntityUid>>(BloodSuckerSystem.FailedBloodPuddlesKey, out var failed, _entManager))
            failed.Remove(target);

        return HTNOperatorStatus.Finished;
    }

    private void RememberFailedPuddle(NPCBlackboard blackboard, EntityUid puddle)
    {
        if (!blackboard.TryGetValue<HashSet<EntityUid>>(BloodSuckerSystem.FailedBloodPuddlesKey, out var failed, _entManager))
        {
            failed = new HashSet<EntityUid>();
            blackboard.SetValue(BloodSuckerSystem.FailedBloodPuddlesKey, failed);
        }

        failed.Add(puddle);
    }
}
