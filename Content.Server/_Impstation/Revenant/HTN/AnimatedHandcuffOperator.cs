using Content.Server.Interaction;
using Content.Server.NPC;
using Content.Server.NPC.HTN;
using Content.Server.NPC.HTN.PrimitiveTasks;
using Content.Shared.CombatMode;
using Content.Shared.Cuffs.Components;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Timing;

namespace Content.Server._Impstation.Revenant.HTN;

/// <summary>
/// Cuffs a target using an animated handcuff entity as both user and used item.
/// <see cref="InteractWithOperator"/> fails because <see cref="SharedInteractionSystem.TryGetUsedEntity"/> requires hands.
/// </summary>
public sealed partial class AnimatedHandcuffOperator : HTNOperator
{
    [Dependency] private IEntityManager _entManager = default!;
    private SharedDoAfterSystem _doAfterSystem = default!;

    [DataField(required: true)]
    public string TargetKey = default!;

    [DataField]
    public bool ExpectDoAfter = true;

    public string CurrentDoAfter = "CurrentAnimatedCuffDoAfter";

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
        _doAfterSystem = sysManager.GetEntitySystem<SharedDoAfterSystem>();
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

        ushort nextId = 0;
        if (_entManager.TryGetComponent<DoAfterComponent>(owner, out var doAfter))
        {
            if (blackboard.TryGetValue<ushort>(CurrentDoAfter, out var doAfterId, _entManager))
            {
                return _doAfterSystem.GetStatus(owner, doAfterId, null) switch
                {
                    DoAfterStatus.Running => HTNOperatorStatus.Continuing,
                    DoAfterStatus.Finished => HTNOperatorStatus.Finished,
                    _ => HTNOperatorStatus.Failed
                };
            }

            nextId = doAfter.NextId;
        }

        if (_entManager.TryGetComponent<UseDelayComponent>(owner, out var useDelay)
            && _entManager.System<UseDelaySystem>().IsDelayed((owner, useDelay))
            || !blackboard.TryGetValue<EntityUid>(TargetKey, out var target, _entManager)
            || !_entManager.TryGetComponent<TransformComponent>(target, out var targetXform)
            || !_entManager.HasComponent<HandcuffComponent>(owner))
        {
            return HTNOperatorStatus.Continuing;
        }

        if (_entManager.TryGetComponent<CombatModeComponent>(owner, out var combatMode))
            _entManager.System<SharedCombatModeSystem>().SetInCombatMode(owner, false, combatMode);

        var interaction = _entManager.System<InteractionSystem>();
        interaction.InteractDoAfter(owner, owner, target, targetXform.Coordinates, canReach: true);

        if (doAfter != null && nextId != doAfter.NextId)
        {
            blackboard.SetValue(CurrentDoAfter, nextId);
            return HTNOperatorStatus.Continuing;
        }

        return ExpectDoAfter ? HTNOperatorStatus.Failed : HTNOperatorStatus.Finished;
    }
}
