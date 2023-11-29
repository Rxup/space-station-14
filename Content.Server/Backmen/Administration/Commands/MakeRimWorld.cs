using System.Numerics;
using Content.Server.Administration;
using Content.Server.Atmos;
using Content.Server.Atmos.Components;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Parallax;
using Content.Shared.Administration;
using Content.Shared.Atmos;
using Content.Shared.Gravity;
using Content.Shared.Parallax.Biomes;
using Robust.Server.GameObjects;
using Robust.Shared.Console;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Utility;

namespace Content.Server.Backmen.Administration.Commands;

[AdminCommand(AdminFlags.Mapping)]
public sealed class MakeRimWorld : IConsoleCommand
{
    [Dependency] private readonly IEntitySystemManager _system = default!;
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    public string Command => "makerimworld";

    public string Description => "Создаёт новую карту с биомом Continental и спавнит на нём \"Спавн\"";

    public string Help => "makerimworld";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        var _prototypeManager = IoCManager.Resolve<IPrototypeManager>();
        var _random = IoCManager.Resolve<IRobustRandom>();
        if (!_prototypeManager.TryIndex<BiomeTemplatePrototype>("Continental", out var biomeTemplate))
        {
            return;
        }

        var mapId = _mapManager.NextMapId();
        _mapManager.CreateMap(mapId);

        var mapUid = _mapManager.GetMapEntityId(mapId);
        var grid = _entityManager.EnsureComponent<MapGridComponent>(mapUid);
        MetaDataComponent? metadata = null;

        var biome = _entityManager.EnsureComponent<BiomeComponent>(mapUid);

        var biomeSystem = _entityManager.System<BiomeSystem>();
        biomeSystem.SetSeed(biome, _random.Next());
        biomeSystem.SetTemplate(biome, biomeTemplate);
        biomeSystem.AddMarkerLayer(biome, "Carps");
        biomeSystem.AddMarkerLayer(biome, "OreTin");
        biomeSystem.AddMarkerLayer(biome, "OreGold");
        biomeSystem.AddMarkerLayer(biome, "OreSilver");
        biomeSystem.AddMarkerLayer(biome, "OrePlasma");
        biomeSystem.AddMarkerLayer(biome, "OreUranium");
        biomeSystem.AddTemplate(biome, "Loot", _prototypeManager.Index<BiomeTemplatePrototype>("Caves"), 1);
        _entityManager.Dirty(biome);

        var gravity = _entityManager.EnsureComponent<GravityComponent>(mapUid);
        gravity.Enabled = true;
        _entityManager.Dirty(gravity, metadata);

        var light = _entityManager.EnsureComponent<MapLightComponent>(mapUid);
        light.AmbientLightColor = Color.FromHex("#2b3143");
        _entityManager.Dirty(light, metadata);

        var atmos = _entityManager.EnsureComponent<MapAtmosphereComponent>(mapUid);

        var moles = new float[Atmospherics.AdjustedNumberOfGases];
        moles[(int) Gas.Oxygen] = 21.824779f;
        moles[(int) Gas.Nitrogen] = 82.10312f;

        var mixture = new GasMixture(2500)
        {
            Temperature = 293.15f,
            Moles = moles,
        };

        _entityManager.System<AtmosphereSystem>().SetMapAtmosphere(mapUid, false, mixture, atmos);
        _entityManager.EnsureComponent<MapGridComponent>(mapUid);
        var preloadArea = new Vector2(32f, 32f);
        var mapPos = new MapCoordinates(new Vector2(0f, 0f), mapId);
        var targetArea = new Box2(mapPos.Position - preloadArea, mapPos.Position + preloadArea);
        biomeSystem.Preload(mapUid, biome, targetArea);
        if (_system.GetEntitySystem<MapLoaderSystem>()
            .TryLoad(mapId, "Maps/Backmen/Grids/RimWorldSpawn.yml", out _))
        {
            AttachedGrid( mapUid);
        }
    }

    private void AttachedGrid(EntityUid mapUid)
    {
        if (!_entityManager.TryGetComponent<BiomeComponent>(mapUid, out var biome) ||
            !_entityManager.TryGetComponent<MapGridComponent>(mapUid, out var biomeGrid))
        {
            return;
        }

        var tiles = new List<(Vector2i Index, Tile Tile)>();
        var aabb = new Box2(-32, -32, 32, 32);
        var biomeSystem = _entityManager.System<BiomeSystem>();
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
        biomeGrid.SetTiles(tiles);
    }

    private const byte ChunkSize = 8;
}
