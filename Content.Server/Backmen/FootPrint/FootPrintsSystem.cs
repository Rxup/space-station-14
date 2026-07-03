using System.Threading;
using System.Threading.Tasks;
using Content.Server.Atmos.Components;
using Content.Shared.Backmen.CCVar;
using Content.Shared.Inventory;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Backmen.FootPrint;
using Content.Shared.Chemistry.Components.SolutionManager;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Gravity;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Standing;
using Robust.Shared.Configuration;
using Robust.Shared.Containers;
using Robust.Shared.CPUJob.JobQueues;
using Robust.Shared.CPUJob.JobQueues.Queues;
using Robust.Shared.Map;
using Robust.Shared.Random;

namespace Content.Server.Backmen.FootPrint;

public sealed partial class FootPrintsSystem : EntitySystem
{

    [Dependency] private SharedMapSystem _map = default!;    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private InventorySystem _inventorySystem = default!;

    [Dependency] private SharedSolutionContainerSystem _solutionSystem = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedGravitySystem _gravity = default!;
    [Dependency] private StandingStateSystem _standing = default!;

    [Dependency] private IConfigurationManager _configuration = default!;

    private EntityQuery<TransformComponent> _transformQuery;
    private EntityQuery<MobThresholdsComponent> _mobThresholdQuery;
    private EntityQuery<AppearanceComponent> _appearanceQuery;
    private EntityQuery<InventoryComponent> _inventoryQuery;
    private EntityQuery<ContainerManagerComponent> _containerManagerQuery;
    private EntityQuery<PullableComponent> _pullableQuery;

    private bool _footprintEnabled = false;
    private readonly HashSet<EntityUid> _pendingFootprintMoves = new();

    public override void Initialize()
    {
        base.Initialize();

        _transformQuery = GetEntityQuery<TransformComponent>();
        _mobThresholdQuery = GetEntityQuery<MobThresholdsComponent>();
        _appearanceQuery = GetEntityQuery<AppearanceComponent>();
        _inventoryQuery = GetEntityQuery<InventoryComponent>();
        _containerManagerQuery = GetEntityQuery<ContainerManagerComponent>();
        _pullableQuery = GetEntityQuery<PullableComponent>();

        Subs.CVar(_configuration, CCVars.EnableFootPrints, value => _footprintEnabled = value, true);

        SubscribeLocalEvent<FootPrintsComponent, ComponentStartup>(OnStartupComponent);
        SubscribeLocalEvent<FootPrintsComponent, MoveEvent>(OnMove);
        SubscribeLocalEvent<PullerComponent, MoveEvent>(OnPullerMove);
    }

    private const double ActionJobTime = 0.005;
    private readonly JobQueue _actionJobQueue = new(ActionJobTime);

    private void OnStartupComponent(EntityUid uid, FootPrintsComponent comp, ComponentStartup args)
    {
        comp.StepSize += _random.NextFloat(-0.05f, 0.05f);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        _actionJobQueue.Process();
    }

    public sealed class FootprintsMoveProcess(
        Entity<FootPrintsComponent> ent,
        FootPrintsSystem system,
        double maxTime,
        CancellationToken cancellation = default)
        : Job<object>(maxTime, cancellation)
    {
        protected override async Task<object?> Process()
        {
            try
            {
                system.MoveProccessing(ent);
            }
            finally
            {
                system._pendingFootprintMoves.Remove(ent.Owner);
            }

            return null;
        }
    }

    private void OnMove(EntityUid uid, FootPrintsComponent comp, ref MoveEvent args)
    {
        TryEnqueueFootprintMove(uid, comp);
    }

    private void OnPullerMove(EntityUid uid, PullerComponent puller, ref MoveEvent args)
    {
        if (puller.Pulling is not { } pulled || !TryComp<FootPrintsComponent>(pulled, out var comp))
            return;

        TryEnqueueFootprintMove(pulled, comp);
    }

    private void TryEnqueueFootprintMove(EntityUid uid, FootPrintsComponent comp)
    {
        if (!_footprintEnabled || TerminatingOrDeleted(uid))
            return;

        if (!HasVisibleFluidPrints(comp))
        {
            if (comp.PrintsColor.A > 0f)
                comp.PrintsColor = comp.PrintsColor.WithAlpha(0f);

            return;
        }

        if (!_pendingFootprintMoves.Add(uid))
            return;

        _actionJobQueue.EnqueueJob(new FootprintsMoveProcess((uid, comp), this, ActionJobTime));
    }

    private void MoveProccessing(Entity<FootPrintsComponent> ent)
    {
        if (TerminatingOrDeleted(ent))
            return;

        var (uid, comp) = ent;
        var transform = Transform(ent);

        if (_gravity.IsWeightless(uid))
            return;

        if (!_mobThresholdQuery.TryComp(uid, out var mobThreshHolds) ||
            !_map.TryFindGridAt(_transform.GetMapCoordinates((uid, transform)), out var gridUid, out _))
            return;

        if (!HasVisibleFluidPrints(comp))
            return;

        var useDragPrints = UsesDragFootprints(uid, mobThreshHolds);

        var distance = (transform.LocalPosition - comp.StepPos).Length();
        var stepSize = useDragPrints ? comp.DragSize : comp.StepSize;

        if (!(distance > stepSize))
            return;

        comp.RightStep = !comp.RightStep;

        var entity = Spawn(comp.StepProtoId, CalcCoords(gridUid, comp, transform, useDragPrints));
        var footPrintComponent =
            Comp<FootPrintComponent>(entity); // There's NO way there's no footprint component in a FOOTPRINT

        if (!_appearanceQuery.TryComp(entity, out var appearance))
        {
            Del(entity);
            return;
        }

        var state = PickState(uid, useDragPrints);

        _appearance.SetData(entity, FootPrintValue.Rsi, comp.RsiPath, appearance);
        var layer = state switch
        {
            FootPrintVisuals.BareFootPrint => comp.RightStep
                ? comp.RightBarePrint
                : comp.LeftBarePrint,
            FootPrintVisuals.ShoesPrint => comp.ShoesPrint,
            FootPrintVisuals.SuitPrint => comp.SuitPrint,
            FootPrintVisuals.Dragging => _random.Pick(comp.DraggingPrint),
            _ => "error",
        };

        _appearance.SetData(entity, FootPrintValue.Layer, layer, appearance);
        _appearance.SetData(entity, FootPrintVisualState.Color, comp.PrintsColor, appearance);

        if (!_transformQuery.TryComp(entity, out var stepTransform))
            return;

        stepTransform.LocalRotation = useDragPrints
            ? (transform.LocalPosition - comp.StepPos).ToAngle() + Angle.FromDegrees(-90f)
            : transform.LocalRotation + Angle.FromDegrees(180f);

        var newAlpha = ReduceAlpha(comp.PrintsColor.A, comp.ColorReduceAlpha);
        comp.PrintsColor = newAlpha < comp.MinVisibleAlpha
            ? comp.PrintsColor.WithAlpha(0f)
            : comp.PrintsColor.WithAlpha(newAlpha);
        comp.StepPos = transform.LocalPosition;

        if (!TryComp<SolutionManagerComponent>(entity, out _)
            || !_solutionSystem.ResolveSolution(entity,
                footPrintComponent.SolutionName,
                ref footPrintComponent.Solution,
                out var solution)
            || string.IsNullOrWhiteSpace(comp.ReagentToTransfer) || solution.Volume >= 1
            || footPrintComponent.Solution is not { } solutionUid)
            return;

        _solutionSystem.TryAddReagent(solutionUid, comp.ReagentToTransfer, 1, out _);
    }

    private EntityCoordinates CalcCoords(EntityUid uid,
        FootPrintsComponent comp,
        TransformComponent transform,
        bool state)
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

        if (_inventoryQuery.TryComp(uid, out var inventory) &&
            _containerManagerQuery.TryComp(uid, out var containerManager))
        {
            if (_inventorySystem.TryGetSlotEntity(uid, "shoes", out _, inventory, containerManager))
            {
                state = FootPrintVisuals.ShoesPrint;
            }

            if (
                _inventorySystem.TryGetSlotEntity(
                    uid,
                    "outerClothing",
                    out var suit,
                    inventory,
                    containerManager) &&
                HasComp<PressureProtectionComponent>(suit))
            {
                state = FootPrintVisuals.SuitPrint;
            }
        }

        if (dragging)
            state = FootPrintVisuals.Dragging;

        return state;
    }

    private bool UsesDragFootprints(EntityUid uid, MobThresholdsComponent? mobThresholds = null)
    {
        if (mobThresholds == null)
            _mobThresholdQuery.TryComp(uid, out mobThresholds);

        if (mobThresholds != null &&
            mobThresholds.CurrentThresholdState is MobState.Critical or MobState.Dead)
        {
            return true;
        }

        if (_standing.IsDown(uid))
            return true;

        if (_pullableQuery.TryComp(uid, out var pullable) && pullable.Puller != null)
            return true;

        return false;
    }

    private static bool HasVisibleFluidPrints(FootPrintsComponent comp) =>
        comp.PrintsColor.A >= comp.MinVisibleAlpha;

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
