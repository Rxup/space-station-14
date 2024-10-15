using System.Linq;
using Content.Server.Atmos.Components;
using Content.Server.Chemistry.Containers.EntitySystems;
using Content.Server.Chemistry.EntitySystems;
using Content.Shared.Inventory;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Backmen.FootPrints;
using Content.Shared.Chemistry.Components.SolutionManager;
using Robust.Shared.Map;
using Robust.Shared.Random;

namespace Content.Server.Backmen.FootPrints;

public sealed class FootPrintsSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly InventorySystem _inventorySystem = default!;
    [Dependency] private readonly IMapManager _map = default!;

    [Dependency] private readonly SolutionContainerSystem _solutionSystem = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;

   public override void Initialize()
   {
       base.Initialize();
       SubscribeLocalEvent<FootPrintsComponent, ComponentStartup>(OnStartupComponent);
       SubscribeLocalEvent<FootPrintsComponent, MoveEvent>(OnMove);
   }

    private void OnStartupComponent(EntityUid uid, FootPrintsComponent comp, ComponentStartup args)
    {
        comp.StepSize += _random.NextFloat(-0.05f, 0.05f);
    }

    private void OnMove(EntityUid uid, FootPrintsComponent comp, ref MoveEvent args)
    {
        if (comp.PrintsColor.A <= 0f)
            return;

        if (!TryComp<TransformComponent>(uid, out var transform) ||
            !TryComp<MobThresholdsComponent>(uid, out var mobThreshHolds) ||
            !_map.TryFindGridAt(transform.MapPosition, out var gridUid, out _))
            return;

        var dragging = mobThreshHolds.CurrentThresholdState is MobState.Critical or MobState.Dead;
        var distance = (transform.LocalPosition - comp.StepPos).Length();
        var stepSize = dragging ? comp.DragSize : comp.StepSize;

        if (!(distance > stepSize))
            return;

        comp.RightStep = !comp.RightStep;

        var entity = EntityManager.SpawnEntity(
            "Footstep",
            CalcCoords(gridUid, comp, transform, dragging));

        if (!TryComp<FootPrintComponent>(entity, out var footPrintComponent))
            return;

        footPrintComponent.PrintOwner = uid;
        Dirty(footPrintComponent);

        if (TryComp<AppearanceComponent>(entity, out var appearance))
        {
            _appearance.SetData(entity, FootPrintVisualState.State, PickState(uid, dragging), appearance);
            _appearance.SetData(entity, FootPrintVisualState.Color, comp.PrintsColor, appearance);
        }

        if (TryComp<TransformComponent>(entity, out var stepTransform))
        {
            stepTransform.LocalRotation = dragging
                ? (transform.LocalPosition - comp.StepPos).ToAngle() + Angle.FromDegrees(-90f)
                : transform.LocalRotation + Angle.FromDegrees(180f);
        }

        comp.PrintsColor = comp.PrintsColor.WithAlpha(ReduceAlpha(comp.PrintsColor.A, comp.ColorReduceAlpha));
        comp.StepPos = transform.LocalPosition;

        if (!TryComp<SolutionContainerManagerComponent>(entity, out var solutionContainer)
            || !_solutionSystem.TryGetSolution(entity, footPrintComponent.SolutionName, out var entitySolutions, solutionContainer)
            || string.IsNullOrWhiteSpace(comp.ReagentToTransfer)
            || entitySolutions.Volume >= 1)
            return;

        _solutionSystem.TryAddReagent(entity, entitySolutions, comp.ReagentToTransfer, 1, out _);
    }

    private EntityCoordinates CalcCoords(EntityUid uid, FootPrintsComponent comp, TransformComponent transform, bool state)
    {
        if (state)
            return new EntityCoordinates(uid, transform.LocalPosition);

        var offset = comp.RightStep
            ? new Angle(Angle.FromDegrees(180f) + transform.LocalRotation).RotateVec(comp.OffsetPrint)
            : new Angle(transform.LocalRotation).RotateVec(comp.OffsetPrint);

        return new EntityCoordinates(uid, transform.LocalPosition + offset);
    }

    private FootPrintVisuals PickState(EntityUid uid, bool dragging)
    {
        var state = FootPrintVisuals.BareFootPrint;

        if (_inventorySystem.TryGetSlotEntity(uid, "shoes", out _))
        {
            state = FootPrintVisuals.ShoesPrint;
        }

        if (_inventorySystem.TryGetSlotEntity(uid, "outerClothing", out var suit) && TryComp<PressureProtectionComponent>(suit, out _))
        {
            state = FootPrintVisuals.SuitPrint;
        }

        if (dragging)
            state = FootPrintVisuals.Dragging;

        return state;
    }

    private float ReduceAlpha(float alpha, float reductionAmount)
    {
        if (alpha - reductionAmount > 0f)
        {
            alpha -= reductionAmount;
        }
        else
        {
            alpha = 0f;
        }

        return alpha;
    }
}
