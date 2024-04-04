﻿using System.Threading;
using System.Threading.Tasks;
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

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
        _lookup = sysManager.GetEntitySystem<EntityLookupSystem>();
        _pathfinding = sysManager.GetEntitySystem<PathfindingSystem>();
        _mobSystem = sysManager.GetEntitySystem<MobStateSystem>();
        _npcFactonSystem = sysManager.GetEntitySystem<NpcFactionSystem>();
    }

    public override async Task<(bool Valid, Dictionary<string, object>? Effects)> Plan(NPCBlackboard blackboard,
        CancellationToken cancelToken)
    {
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);

        if (!blackboard.TryGetValue<float>(RangeKey, out var range, _entManager))
            return (false, null);

        var psionicQuery = _entManager.GetEntityQuery<PsionicComponent>();
        var xformQuery = _entManager.GetEntityQuery<TransformComponent>();

        var targets = new List<EntityUid>();

        foreach (var entity in _npcFactonSystem.GetNearbyHostiles(owner, range))
        {
            if (!psionicQuery.TryGetComponent(entity, out var psionic))
                continue;

            if (!_mobSystem.IsCritical(entity))
                continue;

            targets.Add(entity);
        }

        foreach (var target in targets)
        {
            if (!xformQuery.TryGetComponent(target, out var xform))
                continue;

            var targetCoords = xform.Coordinates;
            var path = await _pathfinding.GetPath(owner, target, range, cancelToken);
            if (path.Result != PathResult.Path)
            {
                continue;
            }

            return (true, new Dictionary<string, object>()
            {
                { TargetKey, targetCoords },
                { DrainKey, target },
                { PathfindKey, path}
            });
        }

        return (false, null);
    }
}
