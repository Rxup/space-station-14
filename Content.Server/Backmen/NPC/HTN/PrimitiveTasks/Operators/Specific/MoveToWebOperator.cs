using System.Threading;
using System.Threading.Tasks;
using Content.Server.NPC;
using Content.Server.NPC.Components;
using Content.Server.NPC.HTN;
using Content.Server.NPC.HTN.PrimitiveTasks;
using Content.Server.NPC.Pathfinding;
using Content.Server.NPC.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Components;

/// <summary>
/// Moves an NPC along spider webs only.
/// </summary>
public sealed partial class MoveToWebOperator : HTNOperator, IHtnConditionalShutdown
{
    [Dependency] private IEntityManager _entManager = default!;
    private NPCSteeringSystem _steering = default!;
    private PathfindingSystem _pathfind = default!;
    private SharedTransformSystem _transform = default!;

    [DataField("shutdownState")]
    public HTNPlanState ShutdownState { get; private set; } = HTNPlanState.TaskFinished;

    [DataField("pathfindInPlanning")]
    public bool PathfindInPlanning = true;

    [DataField("removeKeyOnFinish")]
    public bool RemoveKeyOnFinish = true;

    [DataField("targetKey")]
    public string TargetKey = "TargetCoordinates";

    [DataField("pathfindKey")]
    public string PathfindKey = NPCBlackboard.PathfindKey;

    [DataField("rangeKey")]
    public string RangeKey = "MovementRange";

    [DataField("stopOnLineOfSight")]
    public bool StopOnLineOfSight;

    private const string MovementCancelToken = "MovementCancelToken";

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
        _pathfind = sysManager.GetEntitySystem<PathfindingSystem>();
        _steering = sysManager.GetEntitySystem<NPCSteeringSystem>();
        _transform = sysManager.GetEntitySystem<SharedTransformSystem>();
    }

    private PathFlags GetWebFlags(NPCBlackboard blackboard)
    {
        return _pathfind.GetFlags(blackboard) | PathFlags.Web | PathFlags.WebOnly;
    }

    public override async Task<(bool Valid, Dictionary<string, object>? Effects)> Plan(NPCBlackboard blackboard,
        CancellationToken cancelToken)
    {
        if (!blackboard.TryGetValue<EntityCoordinates>(TargetKey, out var targetCoordinates, _entManager))
        {
            return (false, null);
        }

        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);

        if (!_entManager.TryGetComponent<TransformComponent>(owner, out var xform) ||
            !_entManager.TryGetComponent<PhysicsComponent>(owner, out _))
            return (false, null);

        if (!_entManager.TryGetComponent<MapGridComponent>(xform.GridUid, out _) ||
            !_entManager.TryGetComponent<MapGridComponent>(_transform.GetGrid(targetCoordinates), out _))
        {
            return (false, null);
        }

        var range = blackboard.GetValueOrDefault<float>(RangeKey, _entManager);

        if (xform.Coordinates.TryDistance(_entManager, targetCoordinates, out var distance) && distance <= range)
        {
            return (true, new Dictionary<string, object>()
            {
                {NPCBlackboard.OwnerCoordinates, blackboard.GetValueOrDefault<EntityCoordinates>(NPCBlackboard.OwnerCoordinates, _entManager)}
            });
        }

        if (!PathfindInPlanning)
        {
            return (true, new Dictionary<string, object>()
            {
                {NPCBlackboard.OwnerCoordinates, targetCoordinates}
            });
        }

        var path = await _pathfind.GetPath(
            owner,
            xform.Coordinates,
            targetCoordinates,
            range,
            cancelToken,
            GetWebFlags(blackboard));

        // WebOnly often fails when the victim tile is not marked as a web nav poly.
        if (path.Result != PathResult.Path)
        {
            var webFlags = _pathfind.GetFlags(blackboard) | PathFlags.Web;
            path = await _pathfind.GetPath(
                owner,
                xform.Coordinates,
                targetCoordinates,
                range,
                cancelToken,
                webFlags);
        }

        if (path.Result != PathResult.Path)
        {
            return (false, null);
        }

        return (true, new Dictionary<string, object>()
        {
            {NPCBlackboard.OwnerCoordinates, targetCoordinates},
            {PathfindKey, path}
        });
    }

    public override void Startup(NPCBlackboard blackboard)
    {
        base.Startup(blackboard);

        blackboard.Remove<EntityCoordinates>(NPCBlackboard.OwnerCoordinates);
        var targetCoordinates = blackboard.GetValue<EntityCoordinates>(TargetKey);
        var uid = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);

        var comp = _steering.Register(uid, targetCoordinates);
        comp.Flags = GetWebFlags(blackboard);
        comp.ArriveOnLineOfSight = StopOnLineOfSight;

        if (blackboard.TryGetValue<float>(RangeKey, out var range, _entManager))
        {
            comp.Range = range;
        }

        if (blackboard.TryGetValue<PathResultEvent>(PathfindKey, out var result, _entManager))
        {
            if (blackboard.TryGetValue<EntityCoordinates>(NPCBlackboard.OwnerCoordinates, out var coordinates, _entManager))
            {
                var mapCoords = _transform.ToMapCoordinates(coordinates);
                _steering.PrunePath(uid, mapCoords, _transform.ToMapCoordinates(targetCoordinates).Position - mapCoords.Position, result.Path);
            }

            comp.CurrentPath = new Queue<PathPoly>(result.Path);
        }
    }

    public override HTNOperatorStatus Update(NPCBlackboard blackboard, float frameTime)
    {
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);

        if (!_entManager.TryGetComponent<NPCSteeringComponent>(owner, out var steering))
            return HTNOperatorStatus.Failed;

        if (ShutdownState == HTNPlanState.PlanFinished && steering.Status == SteeringStatus.Moving)
        {
            return HTNOperatorStatus.Finished;
        }

        return steering.Status switch
        {
            SteeringStatus.InRange => HTNOperatorStatus.Finished,
            SteeringStatus.NoPath => HTNOperatorStatus.Failed,
            SteeringStatus.Moving => HTNOperatorStatus.Continuing,
            _ => throw new ArgumentOutOfRangeException()
        };
    }

    public void ConditionalShutdown(NPCBlackboard blackboard)
    {
        if (blackboard.TryGetValue<CancellationTokenSource>(MovementCancelToken, out var cancelToken, _entManager))
        {
            cancelToken.Cancel();
            blackboard.Remove<CancellationTokenSource>(MovementCancelToken);
        }

        blackboard.Remove<PathResultEvent>(PathfindKey);

        if (RemoveKeyOnFinish)
        {
            blackboard.Remove<EntityCoordinates>(TargetKey);
        }

        _steering.Unregister(blackboard.GetValue<EntityUid>(NPCBlackboard.Owner));
    }
}
