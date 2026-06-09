using System.Threading;
using System.Threading.Tasks;
using Content.Server.Backmen.Psionics.NPC.GlimmerWisp;
using Content.Server.NPC;
using Content.Server.NPC.HTN.PrimitiveTasks;
using Content.Server.NPC.Pathfinding;
using Content.Server.NPC.Systems;
using Robust.Shared.Map;

namespace Content.Server.Backmen.NPC.HTN.PrimitiveTasks.Operators.Specific;

public sealed partial class PickDrainTargetOperator: HTNOperator
{
    [Dependency] private IEntityManager _entManager = default!;

    private EntityLookupSystem _lookup = default!;
    private PathfindingSystem _pathfinding = default!;

    private EntityQuery<GlimmerWispComponent> _wispQuery;

    [DataField("rangeKey", required: true)]
    public string RangeKey = string.Empty;

    [DataField("targetKey", required: true)]
    public string TargetKey = string.Empty;

    [DataField("drainKey")]
    public string DrainKey = string.Empty;

    /// <summary>
    /// Where the pathfinding result will be stored (if applicable). This gets removed after execution.
    /// </summary>
    [DataField("pathfindKey")]
    public string PathfindKey = NPCBlackboard.PathfindKey;

    private GlimmerWispSystem _wispSystem;
    private EntityQuery<TransformComponent> _xformQuery;


    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
        _lookup = sysManager.GetEntitySystem<EntityLookupSystem>();
        _pathfinding = sysManager.GetEntitySystem<PathfindingSystem>();
        _wispQuery = _entManager.GetEntityQuery<GlimmerWispComponent>();
        _xformQuery = _entManager.GetEntityQuery<TransformComponent>();
        _wispSystem = sysManager.GetEntitySystem<GlimmerWispSystem>();
    }

    public override async Task<(bool Valid, Dictionary<string, object>? Effects)> Plan(NPCBlackboard blackboard,
        CancellationToken cancelToken)
    {
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);

        if(_entManager.Deleted(owner) || !_wispQuery.TryComp(owner, out var wispComponent))
            return (false, null);

        if (!blackboard.TryGetValue<float>(RangeKey, out var range, _entManager))
            return (false, null);

        var approachRange = GlimmerWispSystem.DrainRange;
        var bestTarget = EntityUid.Invalid;
        var bestCoords = EntityCoordinates.Invalid;
        PathResultEvent? bestPath = null;
        var bestDistance = float.MaxValue;
        var found = false;

        foreach (var target in _lookup.GetEntitiesInRange(owner, range))
        {
            if (target == owner)
                continue;

            if (!_wispSystem.CanDrain((owner, wispComponent), target, false))
                continue;

            if (!_xformQuery.TryComp(target, out var xform) ||
                !_xformQuery.TryComp(owner, out var ownerXform))
            {
                continue;
            }

            var distance = (ownerXform.WorldPosition - xform.WorldPosition).Length();
            if (distance >= bestDistance)
                continue;

            var path = await _pathfinding.GetPath(owner, target, approachRange, cancelToken);
            if (path.Result != PathResult.Path)
                continue;

            bestTarget = target;
            bestCoords = xform.Coordinates;
            bestPath = path;
            bestDistance = distance;
            found = true;
        }

        if (!found || bestPath == null)
            return (false, null);

        return (true, new Dictionary<string, object>()
        {
            { TargetKey, bestCoords },
            { DrainKey, bestTarget },
            { PathfindKey, bestPath }
        });
    }
}
