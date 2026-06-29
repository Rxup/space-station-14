using Content.Server.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Random;
using Content.Shared.Random.Helpers;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.Chemistry.EntitySystems;

public sealed partial class SolutionRandomFillSystem : EntitySystem
{
    [Dependency] private SharedSolutionContainerSystem _solutionsSystem = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IRobustRandom _random = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RandomFillSolutionComponent, MapInitEvent>(
            OnRandomSolutionFillMapInit,
            after: [typeof(SharedSolutionContainerSystem)]);
    }

    private void OnRandomSolutionFillMapInit(Entity<RandomFillSolutionComponent> entity, ref MapInitEvent args)
    {
        if (entity.Comp.WeightedRandomId == null)
            return;

        var pick = _proto.Index<WeightedRandomFillSolutionPrototype>(entity.Comp.WeightedRandomId).Pick(_random);

        var reagent = pick.reagent;
        var quantity = pick.quantity;

        if (!_proto.HasIndex<ReagentPrototype>(reagent))
        {
            Log.Error($"Tried to add invalid reagent Id {reagent} using SolutionRandomFill.");
            return;
        }

        if (!_solutionsSystem.TryGetSolution(entity.Owner, entity.Comp.Solution, out var target, out _))
        {
            Log.Error(
                $"A random solution fill {entity.Comp.WeightedRandomId} could not find solution {entity.Comp.Solution} on {ToPrettyString(entity)}");
            return;
        }

        if (target!.Value.Comp.Solution.AvailableVolume < quantity)
            Log.Error($"A random solution fill {entity.Comp.WeightedRandomId} tried to put {pick.quantity} of {pick.reagent} into {ToPrettyString(target.Value)} but there was not enough space!");

        _solutionsSystem.TryAddReagent(target.Value, reagent, quantity);
    }
}
