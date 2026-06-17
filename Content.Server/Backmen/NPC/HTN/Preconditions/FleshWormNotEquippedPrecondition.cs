using Content.Shared.Backmen.Flesh;
using Content.Server.NPC;
using Content.Server.NPC.HTN.Preconditions;

namespace Content.Server.Backmen.NPC.HTN.Preconditions;

public sealed partial class FleshWormNotEquippedPrecondition : HTNPrecondition
{
    [Dependency] private IEntityManager _entManager = default!;

    public override bool IsMet(NPCBlackboard blackboard)
    {
        if (!blackboard.TryGetValue<EntityUid>(NPCBlackboard.Owner, out var owner, _entManager))
            return false;

        return !_entManager.TryGetComponent<FleshWormComponent>(owner, out var worm) || !worm.EquipedOn.Valid;
    }
}
