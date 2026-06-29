using Content.Server.Backmen.Vampiric;
using Content.Server.NPC;
using Content.Server.NPC.HTN.Preconditions;

namespace Content.Server.Backmen.NPC.HTN.Preconditions;

public sealed partial class BloodThirstyPrecondition : HTNPrecondition
{
    [Dependency] private IEntityManager _entManager = default!;

    public override bool IsMet(NPCBlackboard blackboard)
    {
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);
        var bloodSucker = _entManager.System<BloodSuckerSystem>();

        if (bloodSucker.NeedsBlood(owner))
            return true;

        blackboard.Remove<HashSet<EntityUid>>(BloodSuckerSystem.FailedBloodPuddlesKey);
        blackboard.Remove<HashSet<EntityUid>>(BloodSuckerSystem.FailedCocoonMealsKey);
        return false;
    }
}
