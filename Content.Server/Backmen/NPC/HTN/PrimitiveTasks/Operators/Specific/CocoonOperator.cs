using Content.Server.Backmen.Cocoon;
using Content.Server.NPC;
using Content.Server.NPC.HTN;
using Content.Server.NPC.HTN.PrimitiveTasks;
using Content.Shared.DoAfter;

namespace Content.Server.Backmen.NPC.HTN.PrimitiveTasks.Operators.Specific;

public sealed partial class CocoonOperator : HTNOperator
{
    [Dependency] private IEntityManager _entManager = default!;
    private CocoonerSystem _cocooner = default!;
    private SharedDoAfterSystem _doAfter = default!;

    [DataField("targetKey", required: true)]
    public string TargetKey = default!;

    public string CurrentDoAfter = "CurrentCocoonDoAfter";

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
        _cocooner = sysManager.GetEntitySystem<CocoonerSystem>();
        _doAfter = sysManager.GetEntitySystem<SharedDoAfterSystem>();
    }

    public override void Startup(NPCBlackboard blackboard)
    {
        blackboard.Remove<ushort>(CurrentDoAfter);
    }

    public override void TaskShutdown(NPCBlackboard blackboard, HTNOperatorStatus status)
    {
        blackboard.Remove<ushort>(CurrentDoAfter);
    }

    public override HTNOperatorStatus Update(NPCBlackboard blackboard, float frameTime)
    {
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);

        if (blackboard.TryGetValue<ushort>(CurrentDoAfter, out var doAfterId, _entManager))
        {
            return _doAfter.GetStatus(owner, doAfterId, null) switch
            {
                DoAfterStatus.Running => HTNOperatorStatus.Continuing,
                DoAfterStatus.Finished => HTNOperatorStatus.Finished,
                _ => HTNOperatorStatus.Failed
            };
        }

        if (!blackboard.TryGetValue<EntityUid>(TargetKey, out var target, _entManager) ||
            !_entManager.EntityExists(target))
        {
            return HTNOperatorStatus.Failed;
        }

        ushort nextId = 0;
        if (_entManager.TryGetComponent<DoAfterComponent>(owner, out var doAfter))
            nextId = doAfter.NextId;

        if (!_cocooner.NPCStartCocooning(owner, target))
            return HTNOperatorStatus.Failed;

        if (_entManager.TryGetComponent<DoAfterComponent>(owner, out doAfter) && nextId != doAfter.NextId)
        {
            blackboard.SetValue(CurrentDoAfter, nextId);
            return HTNOperatorStatus.Continuing;
        }

        return HTNOperatorStatus.Failed;
    }
}
