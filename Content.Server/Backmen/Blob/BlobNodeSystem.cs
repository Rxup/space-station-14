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
using Robust.Shared.Map;
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

    private EntityQuery<BlobTileComponent> _tileQuery;

    public override void Initialize()
    {
        base.Initialize();
        _tileQuery = GetEntityQuery<BlobTileComponent>();

    }

    private const double PulseJobTime = 0.005;
    private readonly JobQueue _pulseJobQueue = new(PulseJobTime);

    public sealed class BlobPulse : Job<object>
    {
        private readonly BlobNodeSystem _system;
        private readonly Entity<BlobNodeComponent> _ent;

        public BlobPulse(BlobNodeSystem system,
            Entity<BlobNodeComponent> ent,
            double maxTime,
            CancellationToken cancellation = default) : base(maxTime, cancellation)
        {
            _system = system;
            _ent = ent;
        }

        public BlobPulse(BlobNodeSystem system,
            Entity<BlobNodeComponent> ent,
            double maxTime,
            IStopwatch stopwatch,
            CancellationToken cancellation = default) : base(maxTime, stopwatch, cancellation)
        {
            _system = system;
            _ent = ent;
        }

        protected override async Task<object?> Process()
        {
            _system.Pulse(_ent);
            return null;
        }
    }

    private void Pulse(Entity<BlobNodeComponent> ent)
    {
        if(TerminatingOrDeleted(ent) || !EntityManager.TransformQuery.TryComp(ent, out var xform))
            return;

        var radius = ent.Comp.PulseRadius;

        var localPos = xform.Coordinates.Position;

        if (!TryComp<MapGridComponent>(xform.GridUid, out var grid))
        {
            return;
        }

        if (!_tileQuery.TryGetComponent(ent, out var blobTileComponent) || blobTileComponent.Core == null)
            return;

        var innerTiles = _map.GetLocalTilesIntersecting(xform.GridUid.Value, grid,
            new Box2(localPos + new Vector2(-radius, -radius), localPos + new Vector2(radius, radius)),
            false)
            .ToArray();

        _random.Shuffle(innerTiles);

        var explain = true;
        foreach (var tileRef in innerTiles)
        {
            foreach (var tile in _map.GetAnchoredEntities(xform.GridUid.Value, grid, tileRef.GridIndices))
            {
                if (!_tileQuery.HasComp(tile))
                    continue;

                var ev = new BlobTileGetPulseEvent
                {
                    Explain = explain
                };
                RaiseLocalEvent(tile, ev);
                explain = false;
            }
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

        var blobFactoryQuery = EntityQueryEnumerator<BlobNodeComponent>();
        while (blobFactoryQuery.MoveNext(out var ent, out var comp))
        {
            if (_gameTiming.CurTime < comp.NextPulse)
                continue;

            if (_tileQuery.TryGetComponent(ent, out var blobTileComponent) && blobTileComponent.Core != null)
            {
                _pulseJobQueue.EnqueueJob(new BlobPulse(this,(ent, comp),PulseJobTime));
            }

            comp.NextPulse = _gameTiming.CurTime + TimeSpan.FromSeconds(comp.PulseFrequency);
        }
    }
}
