using Content.Server.Backmen.Flesh;
using Content.Shared.Backmen.Flesh;
using Content.Server.NPC;
using Content.Server.NPC.HTN;
using Content.Server.NPC.HTN.PrimitiveTasks;

namespace Content.Server.Backmen.NPC.HTN.PrimitiveTasks.Operators.Specific;

public sealed partial class FleshWormPounceOperator : HTNOperator
{
    [Dependency] private IEntityManager _entManager = default!;
    private FleshWormSystem _fleshWorm = default!;

    [DataField("targetKey", required: true)]
    public string TargetKey = default!;

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
        _fleshWorm = sysManager.GetEntitySystem<FleshWormSystem>();
    }

    public override HTNOperatorStatus Update(NPCBlackboard blackboard, float frameTime)
    {
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);

        if (!_entManager.TryGetComponent<FleshWormComponent>(owner, out var worm))
            return HTNOperatorStatus.Failed;

        if (worm.EquipedOn.Valid)
            return HTNOperatorStatus.Continuing;

        if (!worm.PendingPounceTarget.Valid
            && !blackboard.TryGetValue<EntityUid>(TargetKey, out _, _entManager))
        {
            return HTNOperatorStatus.Failed;
        }

        blackboard.TryGetValue<EntityUid>(TargetKey, out var target, _entManager);

        return _fleshWorm.NPCTryPounce(owner, target)
            ? HTNOperatorStatus.Continuing
            : HTNOperatorStatus.Failed;
    }
}
