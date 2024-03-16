using System.Linq;
using System.Numerics;
using Content.Server.Backmen.Blob.Components;
using Content.Shared.Backmen.Blob.Components;
using Robust.Server.GameObjects;
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

    private void Pulse(EntityUid uid, BlobNodeComponent component)
    {
        var xform = Transform(uid);

        var radius = component.PulseRadius;

        var localPos = xform.Coordinates.Position;

        if (!TryComp<MapGridComponent>(xform.GridUid, out var grid))
        {
            return;
        }

        if (!_tileQuery.TryGetComponent(uid, out var blobTileComponent) || blobTileComponent.Core == null)
            return;

        var innerTiles = _map.GetLocalTilesIntersecting(xform.GridUid.Value, grid,
            new Box2(localPos + new Vector2(-radius, -radius), localPos + new Vector2(radius, radius)), false).ToArray();

        _random.Shuffle(innerTiles);

        var explain = true;
        foreach (var tileRef in innerTiles)
        {
            foreach (var ent in _map.GetAnchoredEntities(xform.GridUid.Value, grid, tileRef.GridIndices))
            {
                if (!HasComp<BlobTileComponent>(ent))
                    continue;

                var ev = new BlobTileGetPulseEvent
                {
                    Explain = explain
                };
                RaiseLocalEvent(ent, ev);
                explain = false;
            }
        }

        foreach (var lookupUid in _lookup.GetEntitiesInRange(xform.Coordinates, radius))
        {
            if (!HasComp<BlobMobComponent>(lookupUid))
                continue;
            var ev = new BlobMobGetPulseEvent();
            RaiseLocalEvent(lookupUid, ev);
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var blobFactoryQuery = EntityQueryEnumerator<BlobNodeComponent>();
        while (blobFactoryQuery.MoveNext(out var ent, out var comp))
        {
            if (_gameTiming.CurTime < comp.NextPulse)
                return;

            if (_tileQuery.TryGetComponent(ent, out var blobTileComponent) && blobTileComponent.Core != null)
            {
                Pulse(ent, comp);
            }

            comp.NextPulse = _gameTiming.CurTime + TimeSpan.FromSeconds(comp.PulseFrequency);
        }
    }
}
