using System.Linq;
using Content.Shared.Backmen.CCVar;
using Content.Shared.Backmen.FootPrint;
using Content.Shared.Chemistry.Components.SolutionManager;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Fluids;
using Content.Shared.Fluids.Components;
using Robust.Shared.Configuration;
using Robust.Shared.Physics.Events;

namespace Content.Server.Backmen.FootPrint;

public sealed class PuddleFootPrintsSystem : EntitySystem
{
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solutionContainerSystem = default!;
    [Dependency] private readonly IConfigurationManager _configuration = default!;
    private EntityQuery<AppearanceComponent> _appearanceQuery;
    private EntityQuery<PuddleComponent> _puddleQuery;
    private EntityQuery<FootPrintsComponent> _footPrintsQuery;
    private EntityQuery<SolutionContainerManagerComponent> _solutionContainerManageQuery;

    [ValidatePrototypeId<ReagentPrototype>]
    private const string WaterId = "Water";

    private bool _footprintEnabled = false;

    public override void Initialize()
   {
       base.Initialize();
       SubscribeLocalEvent<PuddleFootPrintsComponent, EndCollideEvent>(OnStepTrigger);

       _appearanceQuery = GetEntityQuery<AppearanceComponent>();
       _puddleQuery = GetEntityQuery<PuddleComponent>();
       _footPrintsQuery = GetEntityQuery<FootPrintsComponent>();
       _solutionContainerManageQuery = GetEntityQuery<SolutionContainerManagerComponent>();

       Subs.CVar(_configuration, CCVars.EnableFootPrints, value => _footprintEnabled = value, true);
   }

    private void OnStepTrigger(EntityUid uid, PuddleFootPrintsComponent comp, ref EndCollideEvent args)
    {
        if(!_footprintEnabled)
            return;

        if (!_appearanceQuery.TryComp(uid, out var appearance) ||
            !_puddleQuery.TryComp(uid, out var puddle) ||
            !_footPrintsQuery.TryComp(args.OtherEntity, out var tripper) ||
            !_solutionContainerManageQuery.TryComp(uid, out var solutionManager))
        {
            return;
        }

        if (!_solutionContainerSystem.ResolveSolution((uid, solutionManager), puddle.SolutionName, ref puddle.Solution, out var solutions))
            return;

        // alles gut!
        var totalSolutionQuantity = solutions.Contents.Sum(sol => (float)sol.Quantity);
        var waterQuantity = (from sol in solutions.Contents where sol.Reagent.Prototype == WaterId select (float) sol.Quantity).FirstOrDefault();

        if (waterQuantity / (totalSolutionQuantity / 100f) > comp.OffPercent)
            return;

        if (solutions.Contents.Count <= 0)
            return;

        tripper.ReagentToTransfer =
            solutions.Contents.Aggregate((l, r) => l.Quantity > r.Quantity ? l : r).Reagent.Prototype;

        if (_appearance.TryGetData(uid, PuddleVisuals.SolutionColor, out var color, appearance) &&
            _appearance.TryGetData(uid, PuddleVisuals.CurrentVolume, out var volume, appearance))
        {
            AddColor((Color)color, (float)volume * comp.SizeRatio, tripper);
        }

        _solutionContainerSystem.RemoveEachReagent(puddle.Solution.Value, 1);
    }

    private void AddColor(Color col, float quantity, FootPrintsComponent comp)
    {
        comp.PrintsColor = comp.ColorQuantity == 0f ? col : Color.InterpolateBetween(comp.PrintsColor, col, 0.2f);
        comp.ColorQuantity += quantity;
    }
}
