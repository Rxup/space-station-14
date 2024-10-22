using System.Linq;
using Content.Shared.Backmen.FootPrint;
using Content.Shared.Chemistry.Components.SolutionManager;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Fluids;
using Robust.Shared.Physics.Events;

namespace Content.Server.Backmen.FootPrint;

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

        if (!_solutionContainerSystem.TryGetSolution((uid, solutionManager), "puddle", out var solutions))
            return;

        // ICH HASSE DICH VERDAMMT NOCH MAL UND ICH HOFFE DU WIRST STERBEN DUMMKOPF
        var contents = solutions.Value.Comp.Solution.Contents;
        var totalSolutionQuantity = contents.Sum(sol => (float)sol.Quantity);
        var waterQuantity = (from sol in contents where sol.Reagent.Prototype == "Water" select (float) sol.Quantity).FirstOrDefault();

        if (waterQuantity / (totalSolutionQuantity / 100f) > comp.OffPercent)
            return;

        if (contents.Count <= 0)
            return;
        tripper.ReagentToTransfer =
            contents.Aggregate((l, r) => l.Quantity > r.Quantity ? l : r).Reagent.Prototype;

        if (_appearance.TryGetData(uid, PuddleVisuals.SolutionColor, out var color, appearance) &&
            _appearance.TryGetData(uid, PuddleVisuals.CurrentVolume, out var volume, appearance))
        {
            AddColor((Color)color, (float)volume * comp.SizeRatio, tripper);
        }

        _solutionContainerSystem.RemoveEachReagent(solutions.Value, 1);
    }

    private void AddColor(Color col, float quantity, FootPrintsComponent comp)
    {
        comp.PrintsColor = comp.ColorQuantity == 0f ? col : Color.InterpolateBetween(comp.PrintsColor, col, 0.2f);
        comp.ColorQuantity += quantity;
    }
}
