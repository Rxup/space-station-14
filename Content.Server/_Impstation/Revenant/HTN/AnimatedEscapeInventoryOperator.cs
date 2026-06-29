using Content.Server.NPC;
using Content.Server.NPC.HTN;
using Content.Server.NPC.HTN.PrimitiveTasks;
using Content.Server.Resist;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Robust.Server.Containers;

namespace Content.Server._Impstation.Revenant.HTN;

/// <summary>
/// Escapes pockets, bags, and other inventories via <see cref="CanEscapeInventoryComponent"/>.
/// </summary>
public sealed partial class AnimatedEscapeInventoryOperator : HTNOperator
{
    [Dependency] private IEntityManager _entManager = default!;
    private ContainerSystem _container = default!;
    private SharedMoverController _mover = default!;

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
        _container = sysManager.GetEntitySystem<ContainerSystem>();
        _mover = sysManager.GetEntitySystem<SharedMoverController>();
    }

    public override HTNOperatorStatus Update(NPCBlackboard blackboard, float frameTime)
    {
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);

        if (!_container.IsEntityInContainer(owner))
            return HTNOperatorStatus.Finished;

        if (!_entManager.TryGetComponent<InputMoverComponent>(owner, out var mover)
            || !_entManager.HasComponent<CanEscapeInventoryComponent>(owner))
        {
            return HTNOperatorStatus.Failed;
        }

        if (_entManager.TryGetComponent<CanEscapeInventoryComponent>(owner, out var escape) && escape.IsEscaping)
            return HTNOperatorStatus.Continuing;

        _mover.SetVelocityDirection((owner, mover), Direction.North, 0, true);
        _mover.SetVelocityDirection((owner, mover), Direction.South, 1, true);

        return HTNOperatorStatus.Continuing;
    }
}
