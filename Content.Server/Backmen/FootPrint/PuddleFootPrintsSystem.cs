using System.Linq;
using Content.Shared.Backmen.CCVar;
using Content.Shared.Backmen.FootPrint;
using Content.Shared.Chemistry.Components.SolutionManager;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Content.Shared.Fluids;
using Content.Shared.Fluids.Components;
using Robust.Shared.Configuration;
using Robust.Shared.Physics.Events;
using Robust.Shared.Prototypes;

namespace Content.Server.Backmen.FootPrint;

public sealed partial class PuddleFootPrintsSystem : EntitySystem
{
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedSolutionContainerSystem _solutionContainerSystem = default!;
    [Dependency] private IConfigurationManager _configuration = default!;
    private EntityQuery<AppearanceComponent> _appearanceQuery;
    private EntityQuery<PuddleComponent> _puddleQuery;
    private EntityQuery<FootPrintsComponent> _footPrintsQuery;
    private EntityQuery<SolutionManagerComponent> _solutionManagerQuery;

    private static readonly ProtoId<ReagentPrototype> WaterId = "Water";

    private bool _footprintEnabled = false;

    public override void Initialize()
   {
       base.Initialize();
       SubscribeLocalEvent<PuddleFootPrintsComponent, EndCollideEvent>(OnStepTrigger);

       _appearanceQuery = GetEntityQuery<AppearanceComponent>();
       _puddleQuery = GetEntityQuery<PuddleComponent>();
       _footPrintsQuery = GetEntityQuery<FootPrintsComponent>();
       _solutionManagerQuery = GetEntityQuery<SolutionManagerComponent>();

       Subs.CVar(_configuration, CCVars.EnableFootPrints, value => _footprintEnabled = value, true);
   }

    private void OnStepTrigger(EntityUid uid, PuddleFootPrintsComponent comp, ref EndCollideEvent args)
    {
        if(!_footprintEnabled)
            return;

        if (!_appearanceQuery.TryComp(uid, out var appearance) ||
            !_puddleQuery.TryComp(uid, out var puddle) ||
            !_footPrintsQuery.TryComp(args.OtherEntity, out var tripper) ||
            !_solutionManagerQuery.HasComponent(uid))
        {
            return;
        }

        if (!_solutionContainerSystem.ResolveSolution(uid, puddle.SolutionName, ref puddle.Solution, out var solutions))
            return;

        if (solutions.Contents.Count <= 0)
            return;

        var volume = solutions.Volume;
        if (volume <= FixedPoint2.Zero)
            return;

        if (volume < comp.MinVolume)
            return;

        var totalSolutionQuantity = volume.Float();
        var waterQuantity = (from sol in solutions.Contents where sol.Reagent.Prototype == WaterId select (float) sol.Quantity).FirstOrDefault();

        if (totalSolutionQuantity <= 0f || waterQuantity / (totalSolutionQuantity / 100f) > comp.OffPercent)
            return;

        tripper.ReagentToTransfer =
            solutions.Contents.Aggregate((l, r) => l.Quantity > r.Quantity ? l : r).Reagent.Prototype;

        if (_appearance.TryGetData(uid, PuddleVisuals.SolutionColor, out var color, appearance) &&
            _appearance.TryGetData(uid, PuddleVisuals.CurrentVolume, out var volumeRatio, appearance))
        {
            AddColor((Color)color, (float)volumeRatio * comp.SizeRatio, tripper);
        }

        if (puddle.Solution is not { } solution)
            return;

        _solutionContainerSystem.RemoveEachReagent(solution, 1);
    }

    private void AddColor(Color col, float quantity, FootPrintsComponent comp)
    {
        comp.PrintsColor = comp.ColorQuantity == 0f ? col : Color.InterpolateBetween(comp.PrintsColor, col, 0.2f);
        comp.ColorQuantity += quantity;
    }
}
