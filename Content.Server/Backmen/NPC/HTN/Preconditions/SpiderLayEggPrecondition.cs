using Content.Server.Backmen.Spider;
using Content.Server.NPC;
using Content.Server.NPC.HTN.Preconditions;

namespace Content.Server.Backmen.NPC.HTN.Preconditions;

public sealed partial class SpiderLayEggPrecondition : HTNPrecondition
{
    [Dependency] private IEntityManager _entManager = default!;

    public override bool IsMet(NPCBlackboard blackboard)
    {
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);
        return _entManager.System<SpiderVampireSystem>().CanLayEgg(owner);
    }
}
