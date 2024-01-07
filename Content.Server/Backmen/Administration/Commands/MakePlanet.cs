using Content.Server.Administration;
using Content.Server.Administration.Logs;
using Content.Shared.Administration;
using Robust.Shared.Console;
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
public sealed class MakePlanet : IConsoleCommand
{
    [Dependency] private readonly IAdminLogManager _adminLogger = default!;
    [Dependency] private readonly IPrototypeManager _protoManager = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;

    public string Command => "makeplanet";

    public string Description => "Создаёт новую карту планеты с указанным биомом и радиусом границы.";

    public string Help => "makeplanet";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {

    private const byte ChunkSize = 8;
    private const int MaxPreloadOffset = 16;

    private void Makeplanet(string inputbiome, int restrictionRange)
    {
        //Prepare to generate Continental planet
        var metadata = _entityManager.System<MetaDataSystem>();
        if (!_protoManager.TryIndex<BiomeTemplatePrototype>(inputbiome, out var biomeTemplate))
        {
            return;
        }
        var biomeSystem = _entityManager.System<BiomeSystem>();
        var mapId = _mapManager.NextMapId();
        _mapManager.CreateMap(mapId);
        var mapUid = _mapManager.GetMapEntityId(mapId);

        var seed = _random.Next();
        var random = new Random(seed);
        var grid = _entityManager.EnsureComponent<MapGridComponent>(mapUid);
        var planetName = SharedSalvageSystem.GetFTLName(_protoManager.Index<DatasetPrototype>("names_borer"), seed);

        var mapPos = new MapCoordinates(new Vector2(0f, 0f), mapId);
        var restriction = _entityManager.AddComponent<RestrictedRangeComponent>(mapUid);
        restriction.Origin = mapPos.Position;
        restriction.Range = restrictionRange;
        biomeSystem.EnsurePlanet(mapUid, _protoManager.Index<BiomeTemplatePrototype>("Continental"), seed);

        var biome = _entityManager.EnsureComponent<BiomeComponent>(mapUid);

        biomeSystem.SetSeed(mapUid, biome, _random.Next());
        biomeSystem.SetTemplate(mapUid, biome, biomeTemplate);
        biomeSystem.AddMarkerLayer(mapUid, biome, "OreIron");
        biomeSystem.AddMarkerLayer(mapUid, biome, "OreUranium");
        biomeSystem.AddMarkerLayer(mapUid, biome, "OrePlasma");
        biomeSystem.AddMarkerLayer(mapUid, biome, "OreQuartz");
        biomeSystem.AddMarkerLayer(mapUid, biome, "OreCoal");
        _entityManager.Dirty(mapUid, biome);

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

    // Generate planet
    private void AttachedGrid(MapId mapId, EntityUid mapUid)
    {
        if (!_entityManager.TryGetComponent<BiomeComponent>(mapUid, out var biome) ||
        !_entityManager.TryGetComponent<MapGridComponent>(mapUid, out var biomeGrid))
        {
            return;
        }
        var mapPos = new MapCoordinates(new Vector2(0f, 0f), mapId);

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
        _entityManager.System<MapSystem>().SetTiles(mapUid, biomeGrid, tiles);
    }
}
