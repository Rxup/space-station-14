using Content.Server.Backmen.Arachne;
using Content.Server.NPC;
using Content.Server.NPC.HTN;
using Content.Server.NPC.HTN.PrimitiveTasks;
using Content.Shared.Backmen.Arachne;
using Content.Shared.Coordinates.Helpers;
using Content.Shared.DoAfter;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.Server.Backmen.NPC.HTN.PrimitiveTasks.Operators.Specific;

/// <summary>
/// Spins a connected web network before idle roaming.
/// </summary>
public sealed partial class SpinWebOperator : HTNOperator
{
    [Dependency] private IEntityManager _entManager = default!;
    private ArachneSystem _arachne = default!;
    private SharedDoAfterSystem _doAfter = default!;

    public string CurrentDoAfter = "CurrentSpinWebDoAfter";

    [DataField]
    public string ExpandRadiusKey = NPCBlackboard.WebExpandRadius;

    [DataField]
    public string MinWebTilesKey = NPCBlackboard.MinWebTiles;

    private EntityQuery<ArachneComponent> _arachneQuery;

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
        _arachne = sysManager.GetEntitySystem<ArachneSystem>();
        _doAfter = sysManager.GetEntitySystem<SharedDoAfterSystem>();
        _arachneQuery = _entManager.GetEntityQuery<ArachneComponent>();
    }

    public override void Startup(NPCBlackboard blackboard)
    {
        blackboard.Remove<ushort>(CurrentDoAfter);
    }

    public override void TaskShutdown(NPCBlackboard blackboard, HTNOperatorStatus status)
    {
        blackboard.Remove<ushort>(CurrentDoAfter);
    }

    public override HTNOperatorStatus Update(NPCBlackboard blackboard, float frameTime)
    {
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);

        if (!_arachneQuery.TryComp(owner, out var arachne))
            return HTNOperatorStatus.Failed;

        if (blackboard.TryGetValue<ushort>(CurrentDoAfter, out var doAfterId, _entManager))
        {
            return _doAfter.GetStatus(owner, doAfterId, null) switch
            {
                DoAfterStatus.Running => HTNOperatorStatus.Continuing,
                DoAfterStatus.Finished => HTNOperatorStatus.Continuing,
                _ => HTNOperatorStatus.Failed
            };
        }

        if (!_entManager.TryGetComponent<TransformComponent>(owner, out var xform))
            return HTNOperatorStatus.Failed;

        var coords = xform.Coordinates.SnapToGrid(_entManager);

        var expandRadius = 2;
        var minWebTiles = 9;

        if (blackboard.TryGetValue<float>(ExpandRadiusKey, out var radius, _entManager))
            expandRadius = (int) radius;

        if (blackboard.TryGetValue<float>(MinWebTilesKey, out var minTiles, _entManager))
            minWebTiles = (int) minTiles;

        expandRadius = Math.Max(expandRadius, 1);
        minWebTiles = Math.Max(minWebTiles, 1);

        var webCount = _arachne.CountWebbedTilesInRadius(coords, expandRadius);

        if (webCount >= minWebTiles && _arachne.IsTileWebbed(coords))
            return HTNOperatorStatus.Finished;

        if (!_arachne.TryGetNextExpandableWebTile(owner, coords, expandRadius, out var target))
            return webCount > 0 ? HTNOperatorStatus.Finished : HTNOperatorStatus.Failed;

        ushort nextId = 0;
        if (_entManager.TryGetComponent<DoAfterComponent>(owner, out var doAfter))
            nextId = doAfter.NextId;

        if (!_arachne.NPCTrySpinWebAt(owner, target, arachne))
            return webCount > 0 ? HTNOperatorStatus.Finished : HTNOperatorStatus.Failed;

        if (_entManager.TryGetComponent<DoAfterComponent>(owner, out doAfter) && nextId != doAfter.NextId)
        {
            blackboard.SetValue(CurrentDoAfter, nextId);
            return HTNOperatorStatus.Continuing;
        }

        return HTNOperatorStatus.Failed;
    }
}

