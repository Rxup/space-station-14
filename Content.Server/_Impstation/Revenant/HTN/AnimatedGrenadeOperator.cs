using Content.Server.NPC;
using Content.Server.NPC.HTN;
using Content.Server.NPC.HTN.PrimitiveTasks;
using Content.Shared.CombatMode;
using Content.Shared.Interaction;
using Content.Shared.Trigger.Components;
using Content.Shared.Trigger.Components.Triggers;
using Content.Shared.Trigger.Systems;
using Content.Shared.Timing;
using Robust.Shared.GameObjects;
namespace Content.Server._Impstation.Revenant.HTN;

/// <summary>
/// Primes an animated grenade when in range of a target.
/// <see cref="InteractWithOperator"/> / <see cref="AltInteractOperator"/> do not work — no hands, and the grenade must use itself.
/// </summary>
public sealed partial class AnimatedGrenadeOperator : HTNOperator
{
    [Dependency] private IEntityManager _entManager = default!;
    private SharedInteractionSystem _interaction = default!;
    private SharedTransformSystem _transform = default!;
    private TriggerSystem _trigger = default!;

    [DataField]
    public string TargetKey = "Target";

    [DataField]
    public string RangeKey = "InteractRange";

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
        _interaction = sysManager.GetEntitySystem<SharedInteractionSystem>();
        _transform = sysManager.GetEntitySystem<SharedTransformSystem>();
        _trigger = sysManager.GetEntitySystem<TriggerSystem>();
    }

    public override HTNOperatorStatus Update(NPCBlackboard blackboard, float frameTime)
    {
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);

        if (_entManager.TryGetComponent<UseDelayComponent>(owner, out var useDelay)
            && _entManager.System<UseDelaySystem>().IsDelayed((owner, useDelay)))
        {
            return HTNOperatorStatus.Continuing;
        }

        if (!blackboard.TryGetValue<EntityUid>(TargetKey, out var target, _entManager)
            || !_entManager.TryGetComponent<TransformComponent>(target, out var targetXform)
            || !_entManager.TryGetComponent<TransformComponent>(owner, out var ownerXform))
        {
            return HTNOperatorStatus.Failed;
        }

        var range = blackboard.GetValueOrDefault<float>(RangeKey, _entManager);
        // Match TargetInRangePrecondition — distance only, not LOS (grenades prime on proximity).
        if (!_transform.InRange(ownerXform.Coordinates, targetXform.Coordinates, range))
            return HTNOperatorStatus.Continuing;

        if (_entManager.TryGetComponent<CombatModeComponent>(owner, out var combatMode))
            _entManager.System<SharedCombatModeSystem>().SetInCombatMode(owner, false, combatMode);

        if (_entManager.HasComponent<ActiveTimerTriggerComponent>(owner) || TryPrimeGrenade(owner))
            return HTNOperatorStatus.Finished;

        return HTNOperatorStatus.Failed;
    }

    private bool TryPrimeGrenade(EntityUid owner)
    {
        if (_entManager.HasComponent<TriggerOnUseComponent>(owner)
            && _trigger.Trigger(owner, owner))
        {
            return true;
        }

        if (_entManager.TryGetComponent<TimerTriggerComponent>(owner, out var timer)
            && _trigger.ActivateTimerTrigger((owner, timer), owner))
        {
            return true;
        }

        if (_interaction.UseInHandInteraction(owner, owner, checkCanUse: false, checkCanInteract: false, checkUseDelay: false))
            return true;

        return _interaction.InteractionActivate(owner, owner, checkCanInteract: false, checkAccess: false);
    }
}
