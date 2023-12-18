using System.Numerics;
using Content.Server.Administration;
using Content.Server.Atmos;
using Content.Server.Atmos.Components;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Parallax;
using Content.Shared.Administration;
using Content.Shared.Atmos;
using Content.Shared.Dataset;
using Content.Shared.Gravity;
using Content.Shared.Movement.Components;
using Content.Shared.Parallax.Biomes;
using Content.Shared.Physics;
using Content.Shared.Salvage;
using Robust.Server.GameObjects;
using Robust.Shared.Console;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Collision.Shapes;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Utility;

namespace Content.Server.Backmen.Administration.Commands;

[AdminCommand(AdminFlags.Mapping)]
public sealed class MakeRimWorld : IConsoleCommand
{
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly IPrototypeManager _protoManager = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    public string Command => "makerimworld";

    public string Description => "Создаёт новую карту с биомом Continental и спавнит на нём \"Спавн\"";

    public string Help => "makerimworld";

    [ValidatePrototypeId<DatasetPrototype>]
    private const string PlanetNames = "names_borer";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        var _metadata = _entityManager.System<MetaDataSystem>();
        if (!_protoManager.TryIndex<BiomeTemplatePrototype>("Continental", out var biomeTemplate))
        {
            return;
        }
        var biomeSystem = _entityManager.System<BiomeSystem>();
        var mapId = _mapManager.NextMapId();
        _mapManager.CreateMap(mapId);
        var mapUid = _mapManager.GetMapEntityId(mapId);



        const int MaxOffset = 256;


        var seed = _random.Next();
        var random = new Random(seed);

        var planetName = SharedSalvageSystem.GetFTLName(_protoManager.Index<DatasetPrototype>(PlanetNames), seed);
        _metadata.SetEntityName(mapUid, planetName);

        var mapPos = new MapCoordinates(new Vector2(0f, 0f), mapId);
        var restriction = _entityManager.AddComponent<RestrictedRangeComponent>(mapUid);
        restriction.Origin = mapPos.Position;
        biomeSystem.EnsurePlanet(mapUid, _protoManager.Index<BiomeTemplatePrototype>("Continental"), seed);

        var biome = _entityManager.EnsureComponent<BiomeComponent>(mapUid);

        biomeSystem.SetSeed(mapUid, biome, _random.Next());
        biomeSystem.SetTemplate(mapUid, biome, biomeTemplate);
        biomeSystem.AddMarkerLayer(mapUid, biome, "Carps");
        biomeSystem.AddMarkerLayer(mapUid, biome, "OreTin");
        biomeSystem.AddMarkerLayer(mapUid, biome, "OreGold");
        biomeSystem.AddMarkerLayer(mapUid, biome, "OreSilver");
        biomeSystem.AddMarkerLayer(mapUid, biome, "OrePlasma");
        biomeSystem.AddMarkerLayer(mapUid, biome, "OreUranium");
        biomeSystem.AddTemplate(mapUid, biome, "Loot", _protoManager.Index<BiomeTemplatePrototype>("Caves"), 1);
        _entityManager.Dirty(mapUid, biome);


        if (_entityManager.System<MapLoaderSystem>()
            .TryLoad(mapId, args.Length == 0 ? "Maps/Backmen/Grids/RimWorldSpawn.yml" : args[0], out _))
        {
            AttachedGrid(mapId, mapUid);
        }

        // Enclose the area
        var boundaryUid = _entityManager.Spawn(null, mapPos);
        var boundaryPhysics = _entityManager.AddComponent<PhysicsComponent>(boundaryUid);
        var cShape = new ChainShape();
        // Don't need it to be a perfect circle, just need it to be loosely accurate.
        cShape.CreateLoop(Vector2.Zero, restriction.Range + 1f, false, count: 4);
        _entityManager.System<FixtureSystem>().TryCreateFixture(
            boundaryUid,
            cShape,
            "boundary",
            collisionLayer: (int) (CollisionGroup.HighImpassable | CollisionGroup.Impassable | CollisionGroup.LowImpassable),
            body: boundaryPhysics);
        _entityManager.System<SharedPhysicsSystem>().WakeBody(boundaryUid, body: boundaryPhysics);
        _entityManager.AddComponent<BoundaryComponent>(boundaryUid);
    }

    private void AttachedGrid(MapId mapId, EntityUid mapUid)
    {
        if (!_entityManager.TryGetComponent<BiomeComponent>(mapUid, out var biome) ||
            !_entityManager.TryGetComponent<MapGridComponent>(mapUid, out var biomeGrid))
        {
            return;
        }
        var mapPos = new MapCoordinates(new Vector2(0f, 0f), mapId);

        const int MaxPreloadOffset  = 16;
        var preloadArea = new Vector2(MaxPreloadOffset, MaxPreloadOffset);
        var targetArea = new Box2(mapPos.Position - preloadArea, mapPos.Position + preloadArea);
        var biomeSystem = _entityManager.System<BiomeSystem>();


        biomeSystem.Preload(mapUid, biome, targetArea);

        var tiles = new List<(Vector2i Index, Tile Tile)>();
        var aabb = new Box2(-MaxPreloadOffset, -MaxPreloadOffset, MaxPreloadOffset, MaxPreloadOffset);

        for (var x = Math.Floor(aabb.Left); x <= Math.Ceiling(aabb.Right); x++)
        {
            for (var y = Math.Floor(aabb.Bottom); y <= Math.Ceiling(aabb.Top); y++)
            {
                var index = new Vector2i((int) x, (int) y);
                var chunk = SharedMapSystem.GetChunkIndices(index, ChunkSize);

                var mod = biome.ModifiedTiles.GetOrNew(chunk * ChunkSize);

                if (!mod.Add(index) || !biomeSystem.TryGetBiomeTile(index, biome.Layers, biome.Seed, biomeGrid, out var tile))
                    continue;

                // If we flag it as modified then the tile is never set so need to do it ourselves.
                tiles.Add((index, tile.Value));
            }
        }
        _entityManager.System<MapSystem>().SetTiles(mapUid,biomeGrid,tiles);
    }

    private const byte ChunkSize = 8;
}
