using Content.Server.Backmen.Vampiric;
using Content.Server.NPC;
using Content.Server.NPC.HTN.Preconditions;

namespace Content.Server.Backmen.NPC.HTN.Preconditions;

/// <summary>
/// Only allow drinking from puddles when there is no drinkable blood left in nearby cocoons.
/// </summary>
public sealed partial class SpiderNoCocoonMealsPrecondition : HTNPrecondition
{
    [Dependency] private IEntityManager _entManager = default!;

    public override bool IsMet(NPCBlackboard blackboard)
    {
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);
        var range = blackboard.GetValueOrDefault<float>(blackboard.GetVisionRadiusKey(_entManager), _entManager);

        HashSet<EntityUid>? failed = null;
        blackboard.TryGetValue(BloodSuckerSystem.FailedCocoonMealsKey, out failed, _entManager);

        return !_entManager.System<BloodSuckerSystem>().HasNearbyDrinkableCocoonMeals(owner, range, failed);
    }
}
