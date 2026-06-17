using Content.Server.Backmen.Spider;
using Content.Server.NPC;
using Content.Server.NPC.HTN;
using Content.Server.NPC.HTN.PrimitiveTasks;
using Content.Shared.Backmen.Spider.Components;
using Content.Shared.DoAfter;

namespace Content.Server.Backmen.NPC.HTN.PrimitiveTasks.Operators.Specific;

public sealed partial class LayEggOperator : HTNOperator
{
    [Dependency] private IEntityManager _entManager = default!;
    private SpiderVampireSystem _spider = default!;
    private SharedDoAfterSystem _doAfter = default!;

    public string CurrentDoAfter = "CurrentLayEggDoAfter";

    private EntityQuery<SpiderVampireComponent> _spiderQuery;

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
        _spider = sysManager.GetEntitySystem<SpiderVampireSystem>();
        _doAfter = sysManager.GetEntitySystem<SharedDoAfterSystem>();
        _spiderQuery = _entManager.GetEntityQuery<SpiderVampireComponent>();
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

        if (!_spiderQuery.TryComp(owner, out var spiderComp))
            return HTNOperatorStatus.Failed;

        if (blackboard.TryGetValue<ushort>(CurrentDoAfter, out var doAfterId, _entManager))
        {
            return _doAfter.GetStatus(owner, doAfterId, null) switch
            {
                DoAfterStatus.Running => HTNOperatorStatus.Continuing,
                DoAfterStatus.Finished => HTNOperatorStatus.Finished,
                _ => HTNOperatorStatus.Failed
            };
        }

        ushort nextId = 0;
        if (_entManager.TryGetComponent<DoAfterComponent>(owner, out var doAfter))
            nextId = doAfter.NextId;

        if (!_spider.NPCTryLayEgg(owner, spiderComp))
            return HTNOperatorStatus.Failed;

        if (_entManager.TryGetComponent<DoAfterComponent>(owner, out doAfter) && nextId != doAfter.NextId)
        {
            blackboard.SetValue(CurrentDoAfter, nextId);
            return HTNOperatorStatus.Continuing;
        }

        return HTNOperatorStatus.Failed;
    }
}
