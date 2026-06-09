using Content.Server.Backmen.Psionics.NPC.GlimmerWisp;
using Content.Server.NPC;
using Content.Server.NPC.HTN.Preconditions;

namespace Content.Server.Backmen.NPC.HTN.Preconditions;

public sealed partial class WispHasDrainTargetPrecondition : HTNPrecondition
{
    [Dependency] private IEntityManager _entManager = default!;

    [DataField("rangeKey")]
    public string RangeKey = "RangedRange";

    public override bool IsMet(NPCBlackboard blackboard)
    {
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);
        var range = blackboard.GetValueOrDefault<float>(RangeKey, _entManager);
        return _entManager.System<GlimmerWispSystem>().HasNearbyDrainTarget(owner, range);
    }
}
