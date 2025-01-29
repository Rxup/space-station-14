using System.Linq;
using Content.Server.Atmos.Components;
using Content.Server.NodeContainer;
using Content.Server.NodeContainer.Nodes;
using Content.Server.Popups;
using Content.Shared.Atmos;
using Content.Shared.Backmen.CCVar; // backmen: pipe stacks
using Content.Shared.Construction.Components;
using JetBrains.Annotations;
using Robust.Server.GameObjects;
using Robust.Shared.Configuration;
using Robust.Shared.Map.Components;

namespace Content.Server.Atmos.EntitySystems;

/// <summary>
/// This handles restricting pipe-based entities from overlapping outlets/inlets with other entities.
/// </summary>
public sealed class PipeRestrictOverlapSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _cfg = default!; // backmen: pipe stacks
    [Dependency] private readonly MapSystem _map = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly TransformSystem _xform = default!;

    private readonly List<EntityUid> _anchoredEntities = new();
    private EntityQuery<NodeContainerComponent> _nodeContainerQuery;
    public bool StrictPipeStacking; // backmen: pipe stacks

    /// <inheritdoc/>
    public override void Initialize()
    {
        SubscribeLocalEvent<PipeRestrictOverlapComponent, AnchorStateChangedEvent>(OnAnchorStateChanged);
        SubscribeLocalEvent<PipeRestrictOverlapComponent, AnchorAttemptEvent>(OnAnchorAttempt);
        Subs.CVar(_cfg, CCVars.StrictPipeStacking, (val) => {StrictPipeStacking = val;}); // backmen: pipe stacks

        _nodeContainerQuery = GetEntityQuery<NodeContainerComponent>();
    }

    private void OnAnchorStateChanged(Entity<PipeRestrictOverlapComponent> ent, ref AnchorStateChangedEvent args)
    {
        if (!args.Anchored)
            return;

        if (HasComp<AnchorableComponent>(ent) && CheckOverlap(ent))
        {
            _popup.PopupEntity(Loc.GetString("pipe-restrict-overlap-popup-blocked", ("pipe", ent.Owner)), ent);
            _xform.Unanchor(ent, Transform(ent));
        }
    }

    private void OnAnchorAttempt(Entity<PipeRestrictOverlapComponent> ent, ref AnchorAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        if (!_nodeContainerQuery.TryComp(ent, out var node))
            return;

        var xform = Transform(ent);
        if (CheckOverlap((ent, node, xform)))
        {
            _popup.PopupEntity(Loc.GetString("pipe-restrict-overlap-popup-blocked", ("pipe", ent.Owner)), ent, args.User);
            args.Cancel();
        }
    }

    [PublicAPI]
    public bool CheckOverlap(EntityUid uid)
    {
        if (!_nodeContainerQuery.TryComp(uid, out var node))
            return false;

        return CheckOverlap((uid, node, Transform(uid)));
    }

    public bool CheckOverlap(Entity<NodeContainerComponent, TransformComponent> ent)
    {
        if (ent.Comp2.GridUid is not { } grid || !TryComp<MapGridComponent>(grid, out var gridComp))
            return false;

        var indices = _map.TileIndicesFor(grid, gridComp, ent.Comp2.Coordinates);
        _anchoredEntities.Clear();
        _map.GetAnchoredEntities((grid, gridComp), indices, _anchoredEntities);

        var takenDirs = PipeDirection.None; // backmen: pipe stacks

        foreach (var otherEnt in _anchoredEntities)
        {
            // this should never actually happen but just for safety
            if (otherEnt == ent.Owner)
                continue;

            if (!_nodeContainerQuery.TryComp(otherEnt, out var otherComp))
                continue;

            // start-backmen: pipe stacks
            var (overlapping, which) = PipeNodesOverlap(ent, (otherEnt, otherComp, Transform(otherEnt)), takenDirs);
            takenDirs |= which;

            if (overlapping)
                return true;
            // end-backmen: pipe stacks
        }

        return false;
    }

    // start-backmen: pipe stacks
    public (bool, PipeDirection) PipeNodesOverlap(Entity<NodeContainerComponent, TransformComponent> ent, Entity<NodeContainerComponent, TransformComponent> other, PipeDirection takenDirs)
    {
        var entDirs = GetAllDirections(ent).ToList();
        var otherDirs = GetAllDirections(other).ToList();
        var entDirsCollapsed = PipeDirection.None;

        foreach (var dir in entDirs)
        {
            entDirsCollapsed |= dir;
            foreach (var otherDir in otherDirs)
            {
                takenDirs |= otherDir;
                if (!StrictPipeStacking)
                    continue;

                if ((dir & otherDir) != 0)
                    return (true, takenDirs);

                if ((dir ^ otherDir) != 0)
                    break;
            }
        }

        // If no strict pipe stacking, then output ("are all entDirs occupied", takenDirs)
        return (!StrictPipeStacking && (takenDirs & entDirsCollapsed) == entDirsCollapsed, takenDirs);
        
    // end-backmen: pipe stacks

        IEnumerable<PipeDirection> GetAllDirections(Entity<NodeContainerComponent, TransformComponent> pipe)
        {
            foreach (var node in pipe.Comp1.Nodes.Values)
            {
                // we need to rotate the pipe manually like this because the rotation doesn't update for pipes that are unanchored.
                if (node is PipeNode pipeNode)
                    yield return pipeNode.OriginalPipeDirection.RotatePipeDirection(pipe.Comp2.LocalRotation);
            }
        }
    }
}
