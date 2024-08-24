using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Content.Server.Atmos.Components;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Backmen.Blob.Components;
using Content.Server.Destructible;
using Content.Server.Emp;
using Content.Server.Explosion.EntitySystems;
using Content.Shared.Backmen.Blob.Components;
using Content.Shared.Backmen.CCVar;
using Content.Shared.Damage;
using Content.Shared.Interaction;
using Content.Shared.Item;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Content.Shared.SubFloor;
using Robust.Server.Audio;
using Robust.Server.GameObjects;
using Robust.Server.Physics;
using Robust.Shared.Audio;
using Robust.Shared.Configuration;
using Robust.Shared.CPUJob.JobQueues;
using Robust.Shared.CPUJob.JobQueues.Queues;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server.Backmen.Blob;

public sealed class BlobCoreActionSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly BlobCoreSystem _blobCoreSystem = default!;
    [Dependency] private readonly ExplosionSystem _explosionSystem = default!;
    [Dependency] private readonly FlammableSystem _flammable = default!;
    [Dependency] private readonly EmpSystem _empSystem = default!;
    [Dependency] private readonly AudioSystem _audioSystem = default!;
    [Dependency] private readonly ITileDefinitionManager _tileDefinitionManager = default!;
    [Dependency] private readonly IGameTiming _gameTiming = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly DamageableSystem _damageableSystem = default!;
    [Dependency] private readonly MapSystem _mapSystem = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    //[Dependency] private readonly GridFixtureSystem _gridFixture = default!;

    private const double ActionJobTime = 0.005;
    private readonly JobQueue _actionJobQueue = new(ActionJobTime);
    private EntityQuery<BlobTileComponent> _tileQuery;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BlobObserverControllerComponent, AfterInteractEvent>(OnInteractController);
        SubscribeLocalEvent<BlobObserverComponent, UserActivateInWorldEvent>(OnInteractTarget);
        _tileQuery = GetEntityQuery<BlobTileComponent>();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        _actionJobQueue.Process();
    }

    public sealed class BlobMouseActionProcess(
        Entity<BlobObserverComponent> ent,
        Entity<BlobCoreComponent> core,
        BlobCoreActionSystem system,
        AfterInteractEvent args,
        double maxTime,
        CancellationToken cancellation = default)
        : Job<object>(maxTime, cancellation)
    {
        protected override async Task<object?> Process()
        {
            system.BlobInteract(ent, core, args);
            return null;
        }
    }

    private readonly HashSet<Entity<MobStateComponent>> _entitiesTrackTiles = new();
    private void BlobInteract(Entity<BlobObserverComponent> observer, Entity<BlobCoreComponent> core, AfterInteractEvent args)
    {
        var location = args.ClickLocation;
        if (!location.IsValid(EntityManager))
            return;

        if (TerminatingOrDeleted(observer) || TerminatingOrDeleted(core))
            return;

        var gridUid = _transform.GetGrid(location);
        if (!HasComp<MapGridComponent>(gridUid))
        {
            location = location.AlignWithClosestGridTile();
            gridUid = _transform.GetGrid(location);
            if (!HasComp<MapGridComponent>(gridUid))
                return;
        }

        if (!TryComp<MapGridComponent>(gridUid, out var grid))
        {
            return;
        }

        #region OnTarget
        if (args.Target != null &&
            !HasComp<BlobMobComponent>(args.Target))
        {
            // This allows blob to attack dead blob tiles.
            if (_tileQuery.TryComp(args.Target.Value, out var tileComp) && tileComp.Core != null)
                return;

            var target = args.Target;

            // Check if the target is adjacent to a tile with BlobCellComponent horizontally or vertically
            var xform = Transform(target.Value);
            var mobTile = _mapSystem.GetTileRef(gridUid.Value, grid, xform.Coordinates);

            var mobAdjacentTiles = new[]
            {
                mobTile.GridIndices.Offset(Direction.East),
                mobTile.GridIndices.Offset(Direction.West),
                mobTile.GridIndices.Offset(Direction.North),
                mobTile.GridIndices.Offset(Direction.South)
            };

            EntityUid? nearTile = null;
            foreach (var indices in mobAdjacentTiles)
            {
                var uid = _mapSystem.GetAnchoredEntities(gridUid.Value, grid, indices)
                    .Where(_tileQuery.HasComponent)
                    .FirstOrNull();

                if (uid == null || CompOrNull<BlobTileComponent>(uid)?.Core == null)
                    continue;

                nearTile = uid;
                break;
            }

            if (nearTile != null && HasComp<DestructibleComponent>(target) && !HasComp<ItemComponent>(target) && !HasComp<SubFloorHideComponent>(target))
            {
                BlobTargetAttack(core, nearTile.Value, target.Value);
                return;
            }
        }
        #endregion

        var node = _blobCoreSystem.GetNearNode(location, core.Comp.TilesRadiusLimit);

        if (node == null)
            return;

        var centerTile = _mapSystem.GetLocalTilesIntersecting(gridUid.Value,
            grid,
            new Box2(location.Position, location.Position),
            false)
            .ToArray();

        var targetTileEmpty = false;
        foreach (var tileRef in centerTile)
        {
            if (tileRef.Tile.IsEmpty)
            {
                targetTileEmpty = true;
                if (!_cfg.GetCVar(CCVars.BlobCanGrowInSpace))
                    return;
            }

            if (_mapSystem.GetAnchoredEntities(gridUid.Value, grid, tileRef.GridIndices).Any(_tileQuery.HasComponent))
            {
                return;
            }
            var pos = _transform.ToMapCoordinates(_mapSystem.ToCoordinates(gridUid.Value, tileRef.GridIndices, grid));

            _entitiesTrackTiles.Clear();

            _lookup.GetEntitiesInRange(pos, EntityLookupSystem.LookupEpsilon, _entitiesTrackTiles);
            foreach (var entityUid in _entitiesTrackTiles)
            {
                if (!HasComp<BlobMobComponent>(entityUid))
                    return;
            }
        }

        var targetTile = _mapSystem.GetTileRef(gridUid.Value, grid, location);

        var adjacentTiles = new[]
        {
            targetTile.GridIndices.Offset(Direction.East),
            targetTile.GridIndices.Offset(Direction.West),
            targetTile.GridIndices.Offset(Direction.North),
            targetTile.GridIndices.Offset(Direction.South)
        };

        EntityUid? fromTile = null;
        foreach (var indices in adjacentTiles)
        {
            var uid = _mapSystem.GetAnchoredEntities(gridUid.Value, grid, indices)
                .Where(_tileQuery.HasComponent)
                .FirstOrNull();

            if (uid == null || CompOrNull<BlobTileComponent>(uid)?.Core == null)
                continue;

            fromTile = uid;
            break;
        }

        if (fromTile == null)
            return;

        // This code doesn't work.
        // If you can debug this, please do and fix it.

        /*var fromTile = adjacentTiles
            .Select(indices=>_mapSystem.GetAnchoredEntities(gridUid.Value, grid, indices).FirstOrNull(_tileQuery.HasComponent))
            .FirstOrDefault(x => x!=null);

        if (fromTile == null)
        {
            var mapPos = _transform.ToMapCoordinates(location);
            var adjacentPos = new[]
            {
                Direction.East,
                Direction.West,
                Direction.North,
                Direction.South
            };

            var tiles = new HashSet<Entity<BlobTileComponent>>();
            foreach (var dir in adjacentPos)
            {
                var pos = mapPos.Offset(dir.ToVec());
                tiles.Clear();
                _lookup.GetEntitiesIntersecting(pos.MapId,
                    new Box2(pos.Position, pos.Position),
                    tiles,
                    LookupFlags.Static);
                if (tiles.Count == 0)
                    continue;
                var tile = tiles.First();
                var tilePos = Transform(tile);

                if (tilePos.GridUid == gridUid || tilePos.GridUid == null ||
                    !TryComp<MapGridComponent>(tilePos.GridUid, out var tileGrid))
                    continue;

                fromTile = tile;

                var locPos = _mapSystem.WorldToLocal(tilePos.GridUid.Value,
                    tileGrid,
                    mapPos.Position + dir.GetOpposite().ToVec());

                _gridFixture.Merge(tilePos.GridUid.Value,
                    gridUid.Value,
                    (Vector2i)locPos,
                    Transform(gridUid.Value).LocalRotation);
            }
        }*/

        var cost = core.Comp.NormalBlobCost;
        if (targetTileEmpty)
        {
            cost *= 3;
        }

        if (!_blobCoreSystem.TryUseAbility(observer, core, core, cost))
            return;

        if (targetTileEmpty)
        {
            var plating = _tileDefinitionManager["Plating"];
            var platingTile = new Tile(plating.TileId);
            _mapSystem.SetTile(gridUid.Value, grid, location, platingTile);
        }

        _blobCoreSystem.TransformBlobTile(null,
            core,
            node,
            core.Comp.NormalBlobTile,
            location,
            transformCost: cost);
    }

    private void BlobTargetAttack(Entity<BlobCoreComponent> ent, Entity<BlobTileComponent?> from, EntityUid target)
    {
        if (!Resolve(from, ref from.Comp))
            return;

        if(ent.Comp.Observer == null)
            return;

        if (!_blobCoreSystem.TryUseAbility(ent.Comp.Observer.Value, ent, ent, ent.Comp.AttackCost))
            return;

        _popup.PopupCursor(Loc.GetString("blob-spent-resource", ("point", ent.Comp.AttackCost)),
            ent.Comp.Observer.Value,
            PopupType.LargeCaution);

        _damageableSystem.TryChangeDamage(target, ent.Comp.ChemDamageDict[ent.Comp.CurrentChem]);

        switch (ent.Comp.CurrentChem)
        {
            case BlobChemType.ExplosiveLattice:
                _explosionSystem.QueueExplosion(target, ent.Comp.BlobExplosive, 4, 1, 6, maxTileBreak: 0);
                break;
            case BlobChemType.ElectromagneticWeb:
            {
                if (_random.Prob(0.2f))
                    _empSystem.EmpPulse(_transform.GetMapCoordinates(target), 3f, 50f, 3f);
                break;
            }
            case BlobChemType.BlazingOil:
            {
                if (TryComp<FlammableComponent>(target, out var flammable))
                {
                    flammable.FireStacks += 2;
                    _flammable.Ignite(target, from, flammable);
                }

                break;
            }
        }

        ent.Comp.NextAction =
            _gameTiming.CurTime + TimeSpan.FromSeconds(ent.Comp.AttackRate);
        _audioSystem.PlayPvs(ent.Comp.AttackSound, from, AudioParams.Default);
    }

    private void OnInteract(EntityUid uid, BlobObserverComponent observerComponent, AfterInteractEvent args)
    {
        if (args.Target == args.User)
            return;

        if (observerComponent.Core == null ||
            !TryComp<BlobCoreComponent>(observerComponent.Core.Value, out var blobCoreComponent))
            return;

        if (_gameTiming.CurTime < blobCoreComponent.NextAction)
            return;

        var location = args.ClickLocation;
        if (!location.IsValid(EntityManager))
            return;

        blobCoreComponent.NextAction = _gameTiming.CurTime + TimeSpan.FromMilliseconds(333); // GCD?

        args.Handled = true;

        _actionJobQueue.EnqueueJob(new BlobMouseActionProcess(
            (uid,observerComponent),
            (observerComponent.Core.Value, blobCoreComponent),
            this,
            args,
            ActionJobTime
        ));
    }
    private void OnInteractTarget(Entity<BlobObserverComponent> ent, ref UserActivateInWorldEvent args)
    {
        var ev = new AfterInteractEvent(args.User, EntityUid.Invalid, args.Target, Transform(args.Target).Coordinates, true);
        OnInteract(ent, ent, ev); // proxy?
        args.Handled = ev.Handled;
    }
    private void OnInteractController(Entity<BlobObserverControllerComponent> ent, ref AfterInteractEvent args)
    {
        var ev = new AfterInteractEvent(args.User, EntityUid.Invalid, args.Target, args.ClickLocation, true);
        OnInteract(ent.Comp.Blob, ent.Comp.Blob, ev); // proxy?
        args.Handled = ev.Handled;
    }
}
