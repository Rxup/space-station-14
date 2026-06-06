using Content.Server.Backmen.Cocoon;
using Content.Server.NPC;
using Content.Server.NPC.HTN.Preconditions;

namespace Content.Server.Backmen.NPC.HTN.Preconditions;

/// <summary>
/// Only allow drinking from puddles when there is nobody nearby to cocoon.
/// </summary>
public sealed partial class SpiderNoCocoonVictimsPrecondition : HTNPrecondition
{
    [Dependency] private IEntityManager _entManager = default!;

    public override bool IsMet(NPCBlackboard blackboard)
    {
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);
        var range = blackboard.GetValueOrDefault<float>(blackboard.GetVisionRadiusKey(_entManager), _entManager);
        return !_entManager.System<CocoonerSystem>().HasNearbyCocoonVictims(owner, range);
    }
}
