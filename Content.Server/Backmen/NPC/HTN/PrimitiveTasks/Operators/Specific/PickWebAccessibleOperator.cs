using System.Collections.Generic;
using System.Linq;

using System.Threading;

using System.Threading.Tasks;

using Content.Server.Backmen.Arachne;

using Content.Server.NPC;

using Content.Server.NPC.HTN.PrimitiveTasks;

using Content.Server.NPC.Pathfinding;



namespace Content.Server.Backmen.NPC.HTN.PrimitiveTasks.Operators.Specific;



/// <summary>

/// Picks a random coordinate reachable only across spider webs.

/// </summary>

public sealed partial class PickWebAccessibleOperator : HTNOperator

{

    [Dependency] private IEntityManager _entManager = default!;

    private PathfindingSystem _pathfinding = default!;

    private ArachneSystem _arachne = default!;



    [DataField("rangeKey", required: true)]

    public string RangeKey = string.Empty;



    [DataField("targetCoordinates")]

    public string TargetCoordinates = "TargetCoordinates";



    [DataField("pathfindKey")]

    public string PathfindKey = NPCBlackboard.PathfindKey;



    public override void Initialize(IEntitySystemManager sysManager)

    {

        base.Initialize(sysManager);

        _pathfinding = sysManager.GetEntitySystem<PathfindingSystem>();

        _arachne = sysManager.GetEntitySystem<ArachneSystem>();

    }



    public override async Task<(bool Valid, Dictionary<string, object>? Effects)> Plan(NPCBlackboard blackboard,

        CancellationToken cancelToken)

    {

        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);



        blackboard.TryGetValue<float>(RangeKey, out var maxRange, _entManager);



        if (maxRange == 0f)

            maxRange = 7f;



        var flags = _pathfinding.GetFlags(blackboard) | PathFlags.Web | PathFlags.WebOnly;



        var path = await _pathfinding.GetRandomPath(

            owner,

            maxRange,

            cancelToken,

            flags: flags);



        if (path.Result == PathResult.Path && path.Path.Count > 1)

        {

            return (true, new Dictionary<string, object>()

            {

                { TargetCoordinates, path.Path.Last().Coordinates },

                { PathfindKey, path }

            });

        }



        var origin = _entManager.GetComponent<TransformComponent>(owner).Coordinates;



        if (_arachne.TryPickNearbyWebTile(origin, maxRange, out var coords))

        {

            return (true, new Dictionary<string, object>()

            {

                { TargetCoordinates, coords }

            });

        }



        return (false, null);

    }

}


