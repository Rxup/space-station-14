using System.Linq;
using System.Numerics;
using Content.Server.Construction.Components;
using Content.Server.Destructible;
using Content.Server.Emp;
using Content.Server.Flash;
using Content.Shared.Backmen.Blob;
using Content.Shared.Backmen.Blob.Components;
using Content.Shared.Damage;
using Content.Shared.Destructible;
using Content.Shared.FixedPoint;
using Robust.Server.Audio;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Map.Components;
using Robust.Shared.Random;

namespace Content.Server.Backmen.Blob;

public sealed class BlobTileSystem : SharedBlobTileSystem
{
    [Dependency] private readonly DamageableSystem _damageableSystem = default!;
    [Dependency] private readonly BlobCoreSystem _blobCoreSystem = default!;
    [Dependency] private readonly AudioSystem _audioSystem = default!;
    [Dependency] private readonly EmpSystem _empSystem = default!;
    [Dependency] private readonly MapSystem _mapSystem = default!;
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    private EntityQuery<BlobCoreComponent> _blobCoreQuery;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BlobTileComponent, DestructionEventArgs>(OnDestruction);
        SubscribeLocalEvent<BlobTileComponent, BlobTileGetPulseEvent>(OnPulsed);
        SubscribeLocalEvent<BlobTileComponent, FlashAttemptEvent>(OnFlashAttempt);
        SubscribeLocalEvent<BlobTileComponent, EntityTerminatingEvent>(OnTerminate);

        _blobCoreQuery = GetEntityQuery<BlobCoreComponent>();
    }

    private void OnTerminate(EntityUid uid, BlobTileComponent component, EntityTerminatingEvent args)
    {
        if (component.Core == null ||
            TerminatingOrDeleted(component.Core.Value))
            return;

        component.Core.Value.Comp.BlobTiles.Remove(uid);
    }

    private void OnFlashAttempt(EntityUid uid, BlobTileComponent component, FlashAttemptEvent args)
    {
        if (args.Used == null || MetaData(args.Used.Value).EntityPrototype?.ID != "GrenadeFlashBang")
            return;

        if (component.BlobTileType == BlobTileType.Normal)
        {
            _damageableSystem.TryChangeDamage(uid, component.FlashDamage);
        }
    }

    private void OnDestruction(EntityUid uid, BlobTileComponent component, DestructionEventArgs args)
    {
        if (component.Core == null || !_blobCoreQuery.TryComp(component.Core.Value, out var blobCoreComponent))
            return;

        if (blobCoreComponent.CurrentChem == BlobChemType.ElectromagneticWeb)
        {
            _empSystem.EmpPulse(_transform.GetMapCoordinates(uid), 3f, 50f, 3f);
        }
    }

    private void OnPulsed(EntityUid uid, BlobTileComponent component, BlobTileGetPulseEvent args)
    {
        if (!TryComp<BlobTileComponent>(uid, out var blobTileComponent) || blobTileComponent.Core == null ||
            !_blobCoreQuery.TryComp(blobTileComponent.Core.Value, out var blobCoreComponent))
            return;

        if (blobCoreComponent.CurrentChem == BlobChemType.RegenerativeMateria)
        {
            var healCore = new DamageSpecifier();
            foreach (var keyValuePair in component.HealthOfPulse.DamageDict)
            {
                healCore.DamageDict.Add(keyValuePair.Key, keyValuePair.Value * 5);
            }

            _damageableSystem.TryChangeDamage(uid, healCore);
        }
        else
        {
            _damageableSystem.TryChangeDamage(uid, component.HealthOfPulse);
        }

        if (!args.Explain)
            return;

        var xform = Transform(uid);

        if (!TryComp<MapGridComponent>(xform.GridUid, out var grid))
        {
            return;
        }

        var nearNode = _blobCoreSystem.GetNearNode(xform.Coordinates, blobCoreComponent.TilesRadiusLimit);

        if (nearNode == null)
            return;

        var mobTile = _mapSystem.GetTileRef(xform.GridUid.Value, grid, xform.Coordinates);

        var mobAdjacentTiles = new[]
        {
            mobTile.GridIndices.Offset(Direction.East),
            mobTile.GridIndices.Offset(Direction.West),
            mobTile.GridIndices.Offset(Direction.North),
            mobTile.GridIndices.Offset(Direction.South),
        };

        _random.Shuffle(mobAdjacentTiles);

        var localPos = xform.Coordinates.Position;

        var radius = 1.0f;

        var innerTiles = _mapSystem.GetLocalTilesIntersecting(xform.GridUid.Value,
                grid,
                new Box2(localPos + new Vector2(-radius, -radius), localPos + new Vector2(radius, radius)))
            .ToArray();

        foreach (var innerTile in innerTiles)
        {
            if (!mobAdjacentTiles.Contains(innerTile.GridIndices))
            {
                continue;
            }

            foreach (var ent in _mapSystem.GetAnchoredEntities(xform.GridUid.Value, grid, innerTile.GridIndices))
            {
                if (!HasComp<DestructibleComponent>(ent) || !HasComp<ConstructionComponent>(ent))
                    continue;
                _damageableSystem.TryChangeDamage(ent, blobCoreComponent.ChemDamageDict[blobCoreComponent.CurrentChem]);
                _audioSystem.PlayPvs(blobCoreComponent.AttackSound, uid, AudioParams.Default);
                args.Explain = true;
                return;
            }

            var spawn = true;
            foreach (var ent in _mapSystem.GetAnchoredEntities(xform.GridUid.Value, grid, innerTile.GridIndices))
            {
                if (!HasComp<BlobTileComponent>(ent))
                    continue;
                spawn = false;
                break;
            }

            if (!spawn)
                continue;

            var location = _mapSystem.ToCoordinates(xform.GridUid.Value, innerTile.GridIndices, grid);

            if (_blobCoreSystem.TransformBlobTile(null,
                    blobTileComponent.Core.Value,
                    nearNode,
                    BlobTileType.Normal,
                    location))
                return;
        }
    }

    protected override void TryUpgrade(Entity<BlobTileComponent> target, Entity<BlobCoreComponent> core)
    {
        var coords = Transform(target).Coordinates;
        var coreComp = core.Comp;
        FixedPoint2 cost;

        var nearNode = _blobCoreSystem.GetNearNode(coords, core.Comp.TilesRadiusLimit);
        if (nearNode == null)
            return;

        switch (target.Comp.BlobTileType)
        {
            case BlobTileType.Normal:
                cost = coreComp.BlobTileCosts[BlobTileType.Strong];

                if (!_blobCoreSystem.TryUseAbility(core, cost, coords))
                    return;

                _blobCoreSystem.TransformBlobTile(
                    target,
                    core,
                    nearNode,
                    BlobTileType.Strong,
                    coords);
                break;

            case BlobTileType.Strong:
                cost = coreComp.BlobTileCosts[BlobTileType.Reflective];

                if (!_blobCoreSystem.TryUseAbility(core, cost, coords))
                    return;

                _blobCoreSystem.TransformBlobTile(
                    target,
                    core,
                    nearNode,
                    BlobTileType.Reflective,
                    coords);
                break;
        }
    }

    /* This work very bad.
     I replace invisible
     wall to teleportation observer
     if he moving away from blob tile */

    // private void OnStartup(EntityUid uid, BlobCellComponent component, ComponentStartup args)
    // {
    //     var xform = Transform(uid);
    //     var radius = 2.5f;
    //     var wallSpacing = 1.5f; // Расстояние между стенами и центральной областью
    //
    //     if (!_map.TryGetGrid(xform.GridUid, out var grid))
    //     {
    //         return;
    //     }
    //
    //     var localpos = xform.Coordinates.Position;
    //
    //     // Получаем тайлы в области с радиусом 2.5
    //     var allTiles = grid.GetLocalTilesIntersecting(
    //         new Box2(localpos + new Vector2(-radius, -radius), localpos + new Vector2(radius, radius))).ToArray();
    //
    //     // Получаем тайлы в области с радиусом 1.5
    //     var innerTiles = grid.GetLocalTilesIntersecting(
    //         new Box2(localpos + new Vector2(-wallSpacing, -wallSpacing), localpos + new Vector2(wallSpacing, wallSpacing))).ToArray();
    //
    //     foreach (var tileref in innerTiles)
    //     {
    //         foreach (var ent in grid.GetAnchoredEntities(tileref.GridIndices))
    //         {
    //             if (HasComp<BlobBorderComponent>(ent))
    //                 QueueDel(ent);
    //             if (HasComp<BlobCellComponent>(ent))
    //             {
    //                 var blockTiles = grid.GetLocalTilesIntersecting(
    //                     new Box2(Transform(ent).Coordinates.Position + new Vector2(-wallSpacing, -wallSpacing),
    //                         Transform(ent).Coordinates.Position + new Vector2(wallSpacing, wallSpacing))).ToArray();
    //                 allTiles = allTiles.Except(blockTiles).ToArray();
    //             }
    //         }
    //     }
    //
    //     var outerTiles = allTiles.Except(innerTiles).ToArray();
    //
    //     foreach (var tileRef in outerTiles)
    //     {
    //         foreach (var ent in grid.GetAnchoredEntities(tileRef.GridIndices))
    //         {
    //             if (HasComp<BlobCellComponent>(ent))
    //             {
    //                 var blockTiles = grid.GetLocalTilesIntersecting(
    //                     new Box2(Transform(ent).Coordinates.Position + new Vector2(-wallSpacing, -wallSpacing),
    //                         Transform(ent).Coordinates.Position + new Vector2(wallSpacing, wallSpacing))).ToArray();
    //                 outerTiles = outerTiles.Except(blockTiles).ToArray();
    //             }
    //         }
    //     }
    //
    //     foreach (var tileRef in outerTiles)
    //     {
    //         var spawn = true;
    //         foreach (var ent in grid.GetAnchoredEntities(tileRef.GridIndices))
    //         {
    //             if (HasComp<BlobBorderComponent>(ent))
    //             {
    //                 spawn = false;
    //                 break;
    //             }
    //         }
    //         if (spawn)
    //             EntityManager.SpawnEntity("BlobBorder", tileRef.GridIndices.ToEntityCoordinates(xform.GridUid.Value, _map));
    //     }
    // }

    // private void OnDestruction(EntityUid uid, BlobTileComponent component, DestructionEventArgs args)
    // {
    //     var xform = Transform(uid);
    //     var radius = 1.0f;
    //
    //     if (!_map.TryGetGrid(xform.GridUid, out var grid))
    //     {
    //         return;
    //     }
    //
    //     var localPos = xform.Coordinates.Position;
    //
    //     var innerTiles = grid.GetLocalTilesIntersecting(
    //         new Box2(localPos + new Vector2(-radius, -radius), localPos + new Vector2(radius, radius)), false).ToArray();
    //
    //     var centerTile = grid.GetLocalTilesIntersecting(
    //         new Box2(localPos, localPos)).ToArray();
    //
    //     innerTiles = innerTiles.Except(centerTile).ToArray();
    //
    //     foreach (var tileref in innerTiles)
    //     {
    //         foreach (var ent in grid.GetAnchoredEntities(tileref.GridIndices))
    //         {
    //             if (!HasComp<BlobTileComponent>(ent))
    //                 continue;
    //             var blockTiles = grid.GetLocalTilesIntersecting(
    //                 new Box2(Transform(ent).Coordinates.Position + new Vector2(-radius, -radius),
    //                     Transform(ent).Coordinates.Position + new Vector2(radius, radius)), false).ToArray();
    //
    //             var tilesToRemove = new List<TileRef>();
    //
    //             foreach (var blockTile in blockTiles)
    //             {
    //                 tilesToRemove.Add(blockTile);
    //             }
    //
    //             innerTiles = innerTiles.Except(tilesToRemove).ToArray();
    //         }
    //     }
    //
    //     foreach (var tileRef in innerTiles)
    //     {
    //         foreach (var ent in grid.GetAnchoredEntities(tileRef.GridIndices))
    //         {
    //             if (HasComp<BlobBorderComponent>(ent))
    //             {
    //                 QueueDel(ent);
    //             }
    //         }
    //     }
    //
    //     EntityManager.SpawnEntity(component.BlobBorder, xform.Coordinates);
    // }
    //
    // private void OnStartup(EntityUid uid, BlobTileComponent component, ComponentStartup args)
    // {
    //     var xform = Transform(uid);
    //     var wallSpacing = 1.0f;
    //
    //     if (!_map.TryGetGrid(xform.GridUid, out var grid))
    //     {
    //         return;
    //     }
    //
    //     var localPos = xform.Coordinates.Position;
    //
    //     var innerTiles = grid.GetLocalTilesIntersecting(
    //         new Box2(localPos + new Vector2(-wallSpacing, -wallSpacing), localPos + new Vector2(wallSpacing, wallSpacing)), false).ToArray();
    //
    //     var centerTile = grid.GetLocalTilesIntersecting(
    //         new Box2(localPos, localPos)).ToArray();
    //
    //     foreach (var tileRef in centerTile)
    //     {
    //         foreach (var ent in grid.GetAnchoredEntities(tileRef.GridIndices))
    //         {
    //             if (HasComp<BlobBorderComponent>(ent))
    //                 QueueDel(ent);
    //         }
    //     }
    //     innerTiles = innerTiles.Except(centerTile).ToArray();
    //
    //     foreach (var tileref in innerTiles)
    //     {
    //         var spaceNear = false;
    //         var hasBlobTile = false;
    //         foreach (var ent in grid.GetAnchoredEntities(tileref.GridIndices))
    //         {
    //             if (!HasComp<BlobTileComponent>(ent))
    //                 continue;
    //             var blockTiles = grid.GetLocalTilesIntersecting(
    //                 new Box2(Transform(ent).Coordinates.Position + new Vector2(-wallSpacing, -wallSpacing),
    //                     Transform(ent).Coordinates.Position + new Vector2(wallSpacing, wallSpacing)), false).ToArray();
    //
    //             var tilesToRemove = new List<TileRef>();
    //
    //             foreach (var blockTile in blockTiles)
    //             {
    //                 if (blockTile.Tile.IsEmpty)
    //                 {
    //                     spaceNear = true;
    //                 }
    //                 else
    //                 {
    //                     tilesToRemove.Add(blockTile);
    //                 }
    //             }
    //
    //             innerTiles = innerTiles.Except(tilesToRemove).ToArray();
    //
    //             hasBlobTile = true;
    //         }
    //
    //         if (!hasBlobTile || spaceNear)
    //             continue;
    //         {
    //             foreach (var ent in grid.GetAnchoredEntities(tileref.GridIndices))
    //             {
    //                 if (HasComp<BlobBorderComponent>(ent))
    //                 {
    //                     QueueDel(ent);
    //                 }
    //             }
    //         }
    //     }
    //
    //     var spaceNearCenter = false;
    //
    //     foreach (var tileRef in innerTiles)
    //     {
    //         var spawn = true;
    //         if (tileRef.Tile.IsEmpty)
    //         {
    //             spaceNearCenter = true;
    //             spawn = false;
    //         }
    //         if (grid.GetAnchoredEntities(tileRef.GridIndices).Any(ent => HasComp<BlobBorderComponent>(ent)))
    //         {
    //             spawn = false;
    //         }
    //         if (spawn)
    //             EntityManager.SpawnEntity(component.BlobBorder, tileRef.GridIndices.ToEntityCoordinates(xform.GridUid.Value, _map));
    //     }
    //     if (spaceNearCenter)
    //     {
    //         EntityManager.SpawnEntity(component.BlobBorder, xform.Coordinates);
    //     }
    // }
}
