using Content.Server.Backmen.Cocoon;
using Content.Server.NPC;
using Content.Server.NPC.HTN.Preconditions;

namespace Content.Server.Backmen.NPC.HTN.Preconditions;

/// <summary>
/// Only melee when a nearby hostile can still fight back.
/// </summary>
public sealed partial class SpiderHasActiveHostilesPrecondition : HTNPrecondition
{
    [Dependency] private IEntityManager _entManager = default!;

    public override bool IsMet(NPCBlackboard blackboard)
    {
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);
        var range = blackboard.GetValueOrDefault<float>("AggroVisionRadius", _entManager);
        return _entManager.System<CocoonerSystem>().HasActiveNearbyHostiles(owner, range);
    }
}
