using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Content.Server.Backmen.Blob.Components;
using Content.Shared.Backmen.Blob.Components;
using Content.Shared.Destructible;
using Robust.Server.GameObjects;
using Robust.Shared.CPUJob.JobQueues;
using Robust.Shared.CPUJob.JobQueues.Queues;
using Robust.Shared.Map.Components;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Backmen.Blob;

public sealed class BlobNodeSystem : EntitySystem
{
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly MapSystem _map = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IGameTiming _gameTiming = default!;
    [Dependency] private readonly BlobCoreSystem _blobCoreSystem = default!;

    private EntityQuery<BlobTileComponent> _tileQuery;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BlobNodeComponent, DestructionEventArgs>(OnDestruction);
        SubscribeLocalEvent<BlobNodeComponent, EntityTerminatingEvent>(OnTerminating);

        _tileQuery = GetEntityQuery<BlobTileComponent>();
    }

    private const double PulseJobTime = 0.005;
    private readonly JobQueue _pulseJobQueue = new(PulseJobTime);

    public sealed class BlobPulse(
        BlobNodeSystem system,
        Entity<BlobNodeComponent> ent,
        double maxTime,
        CancellationToken cancellation = default)
        : Job<object>(maxTime, cancellation)
    {
        protected override async Task<object?> Process()
        {
            system.Pulse(ent);
            return null;
        }
    }

    private void OnTerminating(EntityUid uid, BlobNodeComponent component, ref EntityTerminatingEvent args)
    {
        OnDestruction(uid, component, new DestructionEventArgs());
    }

    private void OnDestruction(EntityUid uid, BlobNodeComponent component, DestructionEventArgs args)
    {
        if (!TryComp<BlobTileComponent>(uid, out var tileComp) ||
            tileComp.BlobTileType != BlobTileType.Node ||
            tileComp.Core == null)
            return;

        foreach (var tile in component.ConnectedTiles)
        {
            if (!TryComp<BlobTileComponent>(tile.Value, out var tileComponent))
                continue;

            _blobCoreSystem.RemoveTileWithReturnCost((tile.Value.Value, tileComponent), tileComp.Core.Value);
        }
    }

    private void Pulse(Entity<BlobNodeComponent> ent)
    {
        if (TerminatingOrDeleted(ent) || !EntityManager.TransformQuery.TryComp(ent, out var xform))
            return;

        var radius = ent.Comp.PulseRadius;

        var localPos = xform.Coordinates.Position;

        if (!TryComp<MapGridComponent>(xform.GridUid, out var grid))
        {
            return;
        }

        if (!_tileQuery.TryGetComponent(ent, out var blobTileComponent) || blobTileComponent.Core == null)
            return;

        var innerTiles = _map.GetLocalTilesIntersecting(xform.GridUid.Value,
                grid,
            new Box2(localPos + new Vector2(-radius, -radius), localPos + new Vector2(radius, radius)),
            false)
            .ToArray();

        _random.Shuffle(innerTiles);

        var explain = true;
        foreach (var tileRef in innerTiles)
        {
            foreach (var tile in _map.GetAnchoredEntities(xform.GridUid.Value, grid, tileRef.GridIndices))
            {
                if (!_tileQuery.TryGetComponent(tile, out var tileComp))
                    continue;

                var ev = new BlobTileGetPulseEvent
                {
                    Explain = explain
                };
                RaiseLocalEvent(tile, ev);
                explain = false;
            }
        }

        foreach (var special in ent.Comp.ConnectedTiles)
        {
            if (special.Value == null || TerminatingOrDeleted(special.Value))
                continue;

            var ev = new BlobSpecialGetPulseEvent();
            RaiseLocalEvent(special.Value.Value, ev);
        }

        foreach (var lookupUid in _lookup.GetEntitiesInRange<BlobMobComponent>(xform.Coordinates, radius))
        {
            var ev = new BlobMobGetPulseEvent();
            RaiseLocalEvent(lookupUid, ev);
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        _pulseJobQueue.Process();

        var blobNodeQuery = EntityQueryEnumerator<BlobNodeComponent>();
        while (blobNodeQuery.MoveNext(out var ent, out var comp))
        {
            if (_gameTiming.CurTime < comp.NextPulse)
                continue;

            if (_tileQuery.TryGetComponent(ent, out var blobTileComponent) && blobTileComponent.Core != null)
            {
                _pulseJobQueue.EnqueueJob(new BlobPulse(this,(ent, comp), PulseJobTime));
            }

            comp.NextPulse = _gameTiming.CurTime + TimeSpan.FromSeconds(comp.PulseFrequency);
        }
    }
}
