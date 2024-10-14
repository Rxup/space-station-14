using System.Linq;
using Content.Shared.Backmen.FootPrints;
using Content.Shared.Chemistry.Components.SolutionManager;
using Content.Shared.Fluids;
using Content.Shared.Chemistry.EntitySystems;
using Robust.Shared.Physics.Events;
namespace Content.Server.Backmen.FootPrints;

public sealed class PuddleFootPrintsSystem : EntitySystem
{
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solutionContainerSystem = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PuddleFootPrintsComponent, EndCollideEvent>(OnStepTrigger);
    }

    private void OnStepTrigger(EntityUid uid, PuddleFootPrintsComponent comp, ref EndCollideEvent args)
    {
        if (!TryComp<AppearanceComponent>(uid, out var appearance) ||
            !TryComp<FootPrintsComponent>(args.OtherEntity, out var tripper) ||
            !TryComp<SolutionContainerManagerComponent>(uid, out var solutionManager))
        {
            return;
        }

        if (!_solutionContainerSystem.TryGetSolution((uid,solutionManager ), "puddle", out var solutions))
            return;

        var totalSolutionQuantity = solutions.Value.Comp.Solution.Contents.Sum(sol => (float)sol.Quantity);
        var waterQuantity = (from sol in solutions.Value.Comp.Solution.Contents where sol.Reagent.Prototype == "Water" select (float) sol.Quantity).FirstOrDefault();

        if (waterQuantity / (totalSolutionQuantity / 100f) > comp.OffPercent)
            return;

        if (_appearance.TryGetData(uid, PuddleVisuals.SolutionColor, out var color, appearance) &&
            _appearance.TryGetData(uid, PuddleVisuals.CurrentVolume, out var volume, appearance))
        {
            AddColor((Color)color, (float)volume * comp.SizeRatio, tripper);
        }
    }

    private void AddColor(Color col, float quantity, FootPrintsComponent comp)
    {
        comp.PrintsColor = comp.ColorQuantity == 0f ? col : Color.InterpolateBetween(comp.PrintsColor, col, 0.4f);
        comp.ColorQuantity += quantity;
    }
}
