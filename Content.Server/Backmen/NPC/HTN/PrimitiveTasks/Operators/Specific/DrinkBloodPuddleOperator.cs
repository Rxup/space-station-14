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

        return HTNOperatorStatus.Finished;
    }
}
