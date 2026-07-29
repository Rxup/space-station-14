using Content.Server.Interaction;
using Content.Server.NPC.Systems;
using Content.Shared.Physics;
using Content.Shared.Storage.Components; // backmen: entity-storage-combat
using Robust.Server.Containers; // backmen: entity-storage-combat

namespace Content.Server.NPC.HTN.Preconditions;

public sealed partial class TargetInLOSPrecondition : HTNPrecondition
{
    [Dependency] private IEntityManager _entManager = default!;
    private InteractionSystem _interaction = default!;
    private ContainerSystem _container = default!; // backmen: entity-storage-combat
    private NPCCombatSystem _npcCombat = default!; // backmen: entity-storage-combat

    [DataField("targetKey")]
    public string TargetKey = "Target";

    [DataField("rangeKey")]
    public string RangeKey = "RangeKey";

    [DataField("opaqueKey")]
    public bool UseOpaqueForLOSChecksKey = true;

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
        _interaction = sysManager.GetEntitySystem<InteractionSystem>();
        _container = sysManager.GetEntitySystem<ContainerSystem>(); // backmen: entity-storage-combat
        _npcCombat = sysManager.GetEntitySystem<NPCCombatSystem>(); // backmen: entity-storage-combat
    }

    public override bool IsMet(NPCBlackboard blackboard)
    {
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);

        if (!blackboard.TryGetValue<EntityUid>(TargetKey, out var target, _entManager))
            return false;

        // start-backmen: entity-storage-combat
        // Aim at the crate/locker if the target is hiding inside and we can break it.
        if (_container.TryGetContainingContainer(target, out var container) &&
            _entManager.HasComponent<EntityStorageComponent>(container.Owner) &&
            _npcCombat.CanDamageEntityStorage(owner, container.Owner))
        {
            target = container.Owner;
        }
        // end-backmen: entity-storage-combat

        var range = blackboard.GetValueOrDefault<float>(RangeKey, _entManager);
        var collisionGroup = UseOpaqueForLOSChecksKey ? CollisionGroup.Opaque : (CollisionGroup.Impassable | CollisionGroup.InteractImpassable);

        return _interaction.InRangeUnobstructed(owner, target, range, collisionGroup);
    }
}
