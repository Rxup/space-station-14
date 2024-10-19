using System.Threading;
using System.Threading.Tasks;
using Content.Server.Backmen.Psionics.NPC.GlimmerWisp;
using Content.Server.NPC;
using Content.Server.NPC.HTN.PrimitiveTasks;
using Content.Server.NPC.Pathfinding;
using Content.Server.NPC.Systems;
using Content.Shared.Backmen.Abilities.Psionics;
using Content.Shared.Mobs.Systems;
using Content.Shared.NPC.Systems;

namespace Content.Server.Backmen.NPC.HTN.PrimitiveTasks.Operators.Specific;

public sealed partial class PickDrainTargetOperator: HTNOperator
{
    [Dependency] private readonly IEntityManager _entManager = default!;
    private NpcFactionSystem _npcFactonSystem = default!;
    private MobStateSystem _mobSystem = default!;

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
        _mobSystem = sysManager.GetEntitySystem<MobStateSystem>();
        _npcFactonSystem = sysManager.GetEntitySystem<NpcFactionSystem>();
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

        foreach (var target in _npcFactonSystem.GetNearbyHostiles(owner, range))
        {
            if (!_wispSystem.CanDrain((owner,wispComponent), target, false))
                continue;

            if (!_xformQuery.TryComp(target, out var xform))
                continue;

            var targetCoords = xform.Coordinates;
            var path = await _pathfinding.GetPath(owner, target, range, cancelToken);
            if (path.Result != PathResult.Path)
                continue;

            return (true, new Dictionary<string, object>()
            {
                { TargetKey, targetCoords },
                { DrainKey, target },
                { PathfindKey, path }
            });
        }
        return (false, null);
    }
}
