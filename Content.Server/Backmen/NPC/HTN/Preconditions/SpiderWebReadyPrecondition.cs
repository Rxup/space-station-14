using Content.Server.Backmen.Arachne;
using Content.Server.NPC;
using Content.Server.NPC.HTN.Preconditions;

namespace Content.Server.Backmen.NPC.HTN.Preconditions;

/// <summary>
/// True when the local web network is large enough and the spider is standing on it.
/// </summary>
public sealed partial class SpiderWebReadyPrecondition : HTNPrecondition
{
    [Dependency] private IEntityManager _entManager = default!;

    [DataField]
    public string ExpandRadiusKey = NPCBlackboard.WebExpandRadius;

    [DataField]
    public string MinWebTilesKey = NPCBlackboard.MinWebTiles;

    public override bool IsMet(NPCBlackboard blackboard)
    {
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);
        var coords = _entManager.GetComponent<TransformComponent>(owner).Coordinates;

        var expandRadius = 2;
        var minWebTiles = 9;

        if (blackboard.TryGetValue<float>(ExpandRadiusKey, out var radius, _entManager))
            expandRadius = (int) radius;

        if (blackboard.TryGetValue<float>(MinWebTilesKey, out var minTiles, _entManager))
            minWebTiles = (int) minTiles;

        return _entManager.System<ArachneSystem>().IsWebNestReady(coords, expandRadius, minWebTiles);
    }
}
