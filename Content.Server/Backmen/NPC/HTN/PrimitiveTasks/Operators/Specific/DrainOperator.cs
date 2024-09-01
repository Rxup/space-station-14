using Content.Server.Backmen.Psionics.NPC.GlimmerWisp;
using Content.Server.NPC;
using Content.Server.NPC.HTN;
using Content.Server.NPC.HTN.PrimitiveTasks;

namespace Content.Server.Backmen.NPC.HTN.PrimitiveTasks.Operators.Specific;

public sealed partial class DrainOperator : HTNOperator
{
    [Dependency] private readonly IEntityManager _entManager = default!;
    private GlimmerWispSystem _wispSystem = default!;

    [DataField("drainKey")]
    public string DrainKey = string.Empty;

    private EntityQuery<GlimmerWispComponent> _wispQuery;

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
        _wispSystem = sysManager.GetEntitySystem<GlimmerWispSystem>();
        _wispQuery = _entManager.GetEntityQuery<GlimmerWispComponent>();
    }

    public override HTNOperatorStatus Update(NPCBlackboard blackboard, float frameTime)
    {
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);
        var target = blackboard.GetValue<EntityUid>(DrainKey);

        if (!target.IsValid() || _entManager.Deleted(target))
            return HTNOperatorStatus.Failed;

        if (!_wispQuery.TryComp(owner, out var wispComp))
            return HTNOperatorStatus.Failed;

        Entity<GlimmerWispComponent> wisp = (owner, wispComp);

        if (_wispSystem.IsDraining(wisp))
            return HTNOperatorStatus.Continuing;

        if (wispComp.DrainTarget == null)
        {
            if (_wispSystem.NPCStartLifedrain(owner, target, wisp))
                return HTNOperatorStatus.Continuing;
            else
                return HTNOperatorStatus.Failed;
        }

        _wispSystem.CancelDrain(wisp);
        return HTNOperatorStatus.Finished;
    }
}
