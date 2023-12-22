using System.Numerics;
using Content.Server.Administration;
using Content.Server.Atmos;
using Content.Server.Atmos.Components;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Parallax;
using Content.Server.Fax;
using Content.Server.Chat.Systems;
using Content.Server.Station.Systems;
using Content.Shared.Paper;
using Content.Shared.Administration;
using Content.Shared.Atmos;
using Content.Shared.Dataset;
using Content.Shared.Gravity;
using Content.Shared.Movement.Components;
using Content.Shared.Parallax.Biomes;
using Content.Shared.Physics;
using Content.Shared.Salvage;
using Robust.Server.GameObjects;
using Robust.Server.Maps;
using Robust.Shared.Console;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Collision.Shapes;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Utility;

namespace Content.Server.Backmen.MakePlanet;

public sealed class MakePlanetSystem : EntitySystem
{
	[Dependency] private readonly ChatSystem _chatSystem = default!;
	[Dependency] private readonly StationSystem _stationSystem = default!;
	[Dependency] private readonly IPrototypeManager _protoManager = default!;
	[Dependency] private readonly IRobustRandom _random = default!;
	[Dependency] private readonly FaxSystem _faxSystem = default!;
	[Dependency] private readonly IEntityManager _entityManager = default!;
	[Dependency] private readonly IMapManager _mapManager = default!;

	private const byte ChunkSize = 8;
	private const int MaxPreloadOffset = 16;

	public void ExecuteColonizeEvent()
	{
		//Prepare to generate Continental planet
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
		var grid = _entityManager.EnsureComponent<MapGridComponent>(mapUid);
		var planetName = SharedSalvageSystem.GetFTLName(_protoManager.Index<DatasetPrototype>("names_borer"), seed);

		// Create FTL point on planet with random name
		var ftlUid = _entityManager.CreateEntityUninitialized("PlanetShuttleMarker", new EntityCoordinates(mapUid, grid.TileSizeHalfVector));
		_metadata.SetEntityName(ftlUid, SharedSalvageSystem.GetFTLName(_protoManager.Index<DatasetPrototype>("names_borer"), seed));
		_entityManager.InitializeAndStartEntity(ftlUid);

		var mapPos = new MapCoordinates(new Vector2(0f, 0f), mapId);
		var restriction = _entityManager.AddComponent<RestrictedRangeComponent>(mapUid);
		restriction.Origin = mapPos.Position;
		restriction.Range = 100;
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


		// Announcement
		var stations = _stationSystem.GetStations();
		if (stations.Count != 0)
		{
			foreach (var station in stations) _chatSystem.DispatchStationAnnouncement(station,
			Loc.GetString("colonize-event-annonce"),
			colorOverride: Color.Yellow);
		}


		// Send new station goal to all faxes which are authorized to receive it
		var faxes = _entityManager.EntityQuery<FaxMachineComponent>();
		var wasSent = false;
		foreach (var fax in faxes)
		{
			if (!fax.ReceiveStationGoal) continue;

			var printout = new FaxPrintout(
			Loc.GetString("colonize-event-goal-paper"),
			Loc.GetString("station-goal-fax-paper-name"),
			null,
			"paper_stamp-centcom",
			new List<StampDisplayInfo>
			{
			new() { StampedName = Loc.GetString("stamp-component-stamped-name-centcom"), StampedColor = Color.FromHex("#BB3232") },
			});
			_faxSystem.Receive(fax.Owner, printout, null, fax);
			wasSent = true;
		}
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
		_entityManager.System<MapSystem>().SetTiles(mapUid,biomeGrid,tiles);
	}
}
