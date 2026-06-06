using Content.Server.Backmen.Psionics.NPC.GlimmerWisp;
using Content.Server.NPC;
using Content.Server.NPC.HTN;
using Content.Server.NPC.HTN.PrimitiveTasks;
using Content.Shared.Mobs.Systems;

namespace Content.Server.Backmen.NPC.HTN.PrimitiveTasks.Operators.Specific;

public sealed partial class DrainOperator : HTNOperator
{
    [Dependency] private IEntityManager _entManager = default!;
    private GlimmerWispSystem _wispSystem = default!;
    private MobStateSystem _mobState = default!;

    [DataField("drainKey")]
    public string DrainKey = string.Empty;

    private EntityQuery<GlimmerWispComponent> _wispQuery;

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
        _wispSystem = sysManager.GetEntitySystem<GlimmerWispSystem>();
        _mobState = sysManager.GetEntitySystem<MobStateSystem>();
        _wispQuery = _entManager.GetEntityQuery<GlimmerWispComponent>();
    }

    public override void TaskShutdown(NPCBlackboard blackboard, HTNOperatorStatus status)
    {
        if (!blackboard.TryGetValue<EntityUid>(NPCBlackboard.Owner, out var owner, _entManager))
            return;

        if (_wispQuery.TryComp(owner, out var wispComp))
            _wispSystem.CancelDrain(wispComp);
    }

    public override HTNOperatorStatus Update(NPCBlackboard blackboard, float frameTime)
    {
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);
        var target = blackboard.GetValue<EntityUid>(DrainKey);

        if (!_wispQuery.TryComp(owner, out var wispComp))
            return HTNOperatorStatus.Failed;

        Entity<GlimmerWispComponent> wisp = (owner, wispComp);

        if (!target.IsValid() || _entManager.Deleted(target))
        {
            _wispSystem.CancelDrain(wispComp);
            RequestFastReplan(owner);
            return HTNOperatorStatus.Failed;
        }

        if (_wispSystem.IsDraining(wispComp))
        {
            if (!_wispSystem.CanDrainTarget(wisp, target))
            {
                _wispSystem.CancelDrain(wispComp);
                RequestFastReplan(owner);
                return HTNOperatorStatus.Finished;
            }

            return HTNOperatorStatus.Continuing;
        }

        if (wispComp.DrainTarget != null)
        {
            _wispSystem.CancelDrain(wispComp);
            RequestFastReplan(owner);
            return HTNOperatorStatus.Finished;
        }

        if (!_wispSystem.CanDrainTarget(wisp, target))
        {
            return _mobState.IsDead(target) ? HTNOperatorStatus.Finished : HTNOperatorStatus.Failed;
        }

        if (!_wispSystem.NPCStartLifedrain(owner, target, wispComp))
        {
            RequestFastReplan(owner);
            return HTNOperatorStatus.Failed;
        }

        return HTNOperatorStatus.Continuing;
    }

    /// <summary>
    /// Allows HTN to immediately pick a new plan instead of waiting on <see cref="HTNComponent.PlanCooldown"/>.
    /// </summary>
    private void RequestFastReplan(EntityUid owner)
    {
        if (_entManager.TryGetComponent(owner, out HTNComponent? htn))
            htn.PlanAccumulator = 0f;
    }
}
