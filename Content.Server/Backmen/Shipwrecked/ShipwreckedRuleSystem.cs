using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Numerics;
using System.Text;
using Content.Server.Access.Systems;
using Content.Server.Atmos;
using Content.Server.Atmos.Components;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Backmen.NPC.Prototypes;
using Content.Server.Backmen.NPC.Systems;
using Content.Server.Backmen.Shipwrecked.Components;
using Content.Server.Backmen.Shipwrecked.Prototypes;
using Content.Server.Body.Components;
using Content.Server.Buckle.Systems;
using Content.Server.Chat.Managers;
using Content.Server.Chat.Systems;
using Content.Server.Construction.Components;
using Content.Server.Destructible;
using Content.Server.Doors.Systems;
using Content.Server.Explosion.EntitySystems;
using Content.Server.Fluids.EntitySystems;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Rules;
using Content.Server.GameTicking.Rules.Components;
using Content.Server.Ghost.Roles.Components;
using Content.Server.Humanoid;
using Content.Server.Maps;
using Content.Server.Mind;
using Content.Server.NPC.Systems;
using Content.Server.Paper;
using Content.Server.Parallax;
using Content.Server.Popups;
using Content.Server.Power.Components;
using Content.Server.Preferences.Managers;
using Content.Server.Procedural;
using Content.Server.Roles;
using Content.Server.RoundEnd;
using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Events;
using Content.Server.Shuttles.Systems;
using Content.Server.Spawners.Components;
using Content.Server.SS220.Chat.Systems;
using Content.Server.Station.Components;
using Content.Server.Station.Systems;
using Content.Server.Storage.Components;
using Content.Server.Warps;
using Content.Server.Zombies;
using Content.Shared.Access.Components;
using Content.Shared.Atmos;
using Content.Shared.Backmen.CCVar;
using Content.Shared.Buckle.Components;
using Content.Shared.Chat;
using Content.Shared.Chemistry.Components;
using Content.Shared.CombatMode.Pacification;
using Content.Shared.Corvax.TTS;
using Content.Shared.Damage;
using Content.Shared.Doors.Components;
using Content.Shared.FixedPoint;
using Content.Shared.Ghost;
using Content.Shared.Gravity;
using Content.Shared.Inventory;
using Content.Shared.Lock;
using Content.Shared.Maps;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Parallax;
using Content.Shared.Parallax.Biomes;
using Content.Shared.Popups;
using Content.Shared.Preferences;
using Content.Shared.Procedural;
using Content.Shared.Random;
using Content.Shared.Random.Helpers;
using Content.Shared.Roles.Jobs;
using Content.Shared.Shuttles.Components;
using Content.Shared.Storage;
using Content.Shared.Zombies;
using Robust.Server.Audio;
using Robust.Server.GameObjects;
using Robust.Server.Maps;
using Robust.Server.Player;
using Robust.Shared.Audio;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server.Backmen.Shipwrecked;

public sealed class ShipwreckedRuleSystem : GameRuleSystem<ShipwreckedRuleComponent>
{
    [Dependency] private readonly IChatManager _chatManager = default!;
    [Dependency] private readonly IConfigurationManager _configurationManager = default!;
    [Dependency] private readonly IGameTiming _gameTiming = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IServerPreferencesManager _preferencesManager = default!;
    [Dependency] private readonly ITileDefinitionManager _tileDefinitionManager = default!;
    [Dependency] private readonly GameTicker _gameTicker = default!;
    [Dependency] private readonly AccessSystem _accessSystem = default!;
    [Dependency] private readonly AtmosphereSystem _atmosphereSystem = default!;
    [Dependency] private readonly AudioSystem _audioSystem = default!;
    [Dependency] private readonly BiomeSystem _biomeSystem = default!;
    [Dependency] private readonly BuckleSystem _buckleSystem = default!;
    [Dependency] private readonly ChatSystem _chatSystem = default!;
    [Dependency] private readonly DamageableSystem _damageableSystem = default!;
    [Dependency] private readonly DestructibleSystem _destructibleSystem = default!;
    [Dependency] private readonly DungeonSystem _dungeonSystem = default!;
    [Dependency] private readonly EntityLookupSystem _entityLookupSystem = default!;
    [Dependency] private readonly ExplosionSystem _explosionSystem = default!;
    [Dependency] private readonly HumanoidAppearanceSystem _humanoidAppearanceSystem = default!;
    [Dependency] private readonly IdCardSystem _cardSystem = default!;
    [Dependency] private readonly InventorySystem _inventorySystem = default!;
    [Dependency] private readonly LockSystem _lockSystem = default!;
    [Dependency] private readonly MapLoaderSystem _mapLoaderSystem = default!;
    [Dependency] private readonly MindSystem _mindSystem = default!;
    [Dependency] private readonly MobStateSystem _mobStateSystem = default!;
    [Dependency] private readonly NPCConversationSystem _npcConversationSystem = default!;
    [Dependency] private readonly NPCSystem _npcSystem = default!;
    [Dependency] private readonly PaperSystem _paperSystem = default!;
    [Dependency] private readonly PhysicsSystem _physicsSystem = default!;
    [Dependency] private readonly PopupSystem _popupSystem = default!;
    [Dependency] private readonly RoundEndSystem _roundEndSystem = default!;
    [Dependency] private readonly ShuttleSystem _shuttleSystem = default!;
    [Dependency] private readonly SmokeSystem _smokeSystem = default!;
    [Dependency] private readonly StationSpawningSystem _stationSpawningSystem = default!;
    [Dependency] private readonly ThrusterSystem _thrusterSystem = default!;
    [Dependency] private readonly TileSystem _tileSystem = default!;
    [Dependency] private readonly TransformSystem _transformSystem = default!;
    [Dependency] private readonly RoleSystem _roleSystem = default!;
    [Dependency] private readonly AirlockSystem _airlockSystem = default!;
    [Dependency] private readonly StationSystem _stationSystem = default!;
    [Dependency] private readonly StationJobsSystem _stationJobsSystem = default!;


    private ISawmill _sawmill = default!;

    public override void Initialize()
    {
        base.Initialize();

        _sawmill = Logger.GetSawmill("shipwrecked");

        SubscribeLocalEvent<FTLCompletedEvent>(OnFTLCompleted);
        SubscribeLocalEvent<FTLStartedEvent>(OnFTLStarted);

        SubscribeLocalEvent<RulePlayerSpawningEvent>(OnPlayersSpawning);
        SubscribeLocalEvent<RoundEndTextAppendEvent>(OnRoundEndText);

        SubscribeLocalEvent<ShipwreckedNPCHecateComponent, MapInitEvent>(OnInitHecate);
        SubscribeLocalEvent<ShipwreckedNPCHecateComponent, ShipwreckedHecateAskGeneratorUnlockEvent>(OnAskGeneratorUnlock);
        SubscribeLocalEvent<ShipwreckedNPCHecateComponent, ShipwreckedHecateAskWeaponsEvent>(OnAskWeapons);
        SubscribeLocalEvent<ShipwreckedNPCHecateComponent, ShipwreckedHecateAskWeaponsUnlockEvent>(OnAskWeaponsUnlock);
        SubscribeLocalEvent<ShipwreckedNPCHecateComponent, ShipwreckedHecateAskStatusEvent>(OnAskStatus);
        SubscribeLocalEvent<ShipwreckedNPCHecateComponent, ShipwreckedHecateAskLaunchEvent>(OnAskLaunch);

        SubscribeLocalEvent<ShipwreckSurvivorComponent, MobStateChangedEvent>(OnSurvivorMobStateChanged);
        SubscribeLocalEvent<ShipwreckSurvivorComponent, BeingGibbedEvent>(OnSurvivorBeingGibbed);
        SubscribeLocalEvent<EntityZombifiedEvent>(OnZombified);

        SubscribeLocalEvent<LoadingMapsEvent>(OnLoadingMaps);
        SubscribeLocalEvent<PlayerBeforeSpawnEvent>(OnBeforeSpawn);
    }

    private void OnBeforeSpawn(PlayerBeforeSpawnEvent ev)
    {
        if (!ev.LateJoin)
        {
            return;
        }
        var query = EntityQueryEnumerator<ShipwreckedRuleComponent, GameRuleComponent>();
        while (query.MoveNext(out var uid, out var shipwrecked, out var gameRule))
        {
            if (!GameTicker.IsGameRuleActive(uid, gameRule))
                continue;

            var points = GetSpawnPoints(shipwrecked);

            var bf = new StringBuilder();

            SpawnTraveller(ev.Player, _random.Pick(points), bf, shipwrecked);
            ev.Handled = true;
        }
    }

    private void OnFTLCompleted(ref FTLCompletedEvent ev)
    {
        var query = EntityQueryEnumerator<ShipwreckedRuleComponent, GameRuleComponent>();
        while (query.MoveNext(out var uid, out var shipwrecked, out var gameRule))
        {
            if (!GameTicker.IsGameRuleActive(uid, gameRule))
                continue;

            if (ev.Entity != shipwrecked.Shuttle)
                continue;

            if (shipwrecked.AllObjectivesComplete)
                _roundEndSystem.EndRound();

            // TODO: See if this is fit for the main game after some testing.
            if (shipwrecked.Destination?.Atmosphere is { } atmos)
                _atmosphereSystem.PatchGridToPlanet(ev.Entity, atmos);
            else
                _atmosphereSystem.UnpatchGridFromPlanet(ev.Entity);
        }
    }

    private void OnFTLStarted(ref FTLStartedEvent ev)
    {
        var query = EntityQueryEnumerator<ShipwreckedRuleComponent, GameRuleComponent>();
        while (query.MoveNext(out var uid, out var shipwrecked, out var gameRule))
        {
            if (!GameTicker.IsGameRuleActive(uid, gameRule))
                continue;

            if (ev.Entity != shipwrecked.Shuttle)
                continue;

            // TODO: See if this is fit for the main game after some testing.
            _atmosphereSystem.UnpatchGridFromPlanet(ev.Entity);

            if (!shipwrecked.AllObjectivesComplete)
                continue;

            // You win!
            _roundEndSystem.EndRound();

        }
    }

    private void OnLoadingMaps(LoadingMapsEvent ev)
    {
        var query = EntityQueryEnumerator<ShipwreckedRuleComponent, GameRuleComponent>();
        while (query.MoveNext(out var uid, out var shipwrecked, out var gameRule))
        {
            if (!GameTicker.IsGameRuleAdded(uid, gameRule))
                continue;

            SpawnPlanet(uid, shipwrecked);

            // This gamemode does not need a station. Revolutionary.
            //ev.Maps.Clear();

            // NOTE: If we could disable the cargo shuttle, emergency shuttle,
            // arrivals station, and centcomm station from loading that would be perfect.
        }
    }

    private void SpawnPlanet(EntityUid uid, ShipwreckedRuleComponent component)
    {
        if (component.PlanetMapId.HasValue && _mapManager.MapExists(component.PlanetMapId.Value))
        {
            return;
        }

        // Most of this code below comes from a protected function in SpawnSalvageMissionJob
        // which really should be made more generic and public...
        //
        // Some of it has been modified to suit my needs.

        var planetMapId = _mapManager.CreateMap();
        var planetMapUid = _mapManager.GetMapEntityId(planetMapId);
        _mapManager.AddUninitializedMap(planetMapId);

        var ftl = _shuttleSystem.AddFTLDestination(planetMapUid, true);
        ftl.Whitelist = new ();

        var planetGrid = EnsureComp<MapGridComponent>(planetMapUid);

        var destination = component.Destination;
        if (destination == null)
            throw new ArgumentException("There is no destination for Shipwrecked.");

        var biome = AddComp<BiomeComponent>(planetMapUid);
        _biomeSystem.SetSeed(biome, _random.Next());
        _biomeSystem.SetTemplate(biome, _prototypeManager.Index<BiomeTemplatePrototype>(destination.BiomePrototype));
        _biomeSystem.AddMarkerLayer(biome, "OreTin");
        _biomeSystem.AddMarkerLayer(biome, "OreGold");
        _biomeSystem.AddMarkerLayer(biome, "OreSilver");
        _biomeSystem.AddMarkerLayer(biome, "OrePlasma");
        _biomeSystem.AddMarkerLayer(biome, "OreUranium");
        _biomeSystem.AddTemplate(biome, "Loot", _prototypeManager.Index<BiomeTemplatePrototype>("Caves"), 1);
        Dirty(planetMapUid, biome);

        // Gravity
        if (destination.Gravity)
        {
            var gravity = EnsureComp<GravityComponent>(planetMapUid);
            gravity.Enabled = true;
            Dirty(planetMapUid, gravity);
        }

        // Atmos
        var atmos = EnsureComp<MapAtmosphereComponent>(planetMapUid);

        if (destination.Atmosphere != null)
        {
            _atmosphereSystem.SetMapAtmosphere(planetMapUid, false, destination.Atmosphere, atmos);
        }
        else
        {
            // Some very generic default breathable atmosphere.
            var moles = new float[Atmospherics.AdjustedNumberOfGases];
            moles[(int) Gas.Oxygen] = 21.824779f;
            moles[(int) Gas.Nitrogen] = 82.10312f;

            var mixture = new GasMixture(2500)
            {
                Temperature = 293.15f,
                Moles = moles,
            };

            _atmosphereSystem.SetMapAtmosphere(planetMapUid, false, mixture, atmos);
        }

        // Lighting
        if (destination.LightColor != null)
        {
            var lighting = EnsureComp<MapLightComponent>(planetMapUid);
            lighting.AmbientLightColor = destination.LightColor.Value;
            Dirty(planetMapUid, lighting);
        }

        _mapManager.DoMapInitialize(planetMapId);
        _mapManager.SetMapPaused(planetMapId, true);

        component.PlanetMapId = planetMapId;
        component.PlanetMap = planetMapUid;
        component.PlanetGrid = planetGrid;
    }

    private async void SpawnPlanetaryStructures(EntityUid uid, ShipwreckedRuleComponent component)
    {
        if (component.Destination == null || component.PlanetMap == null || component.PlanetGrid == null)
            return;

        var origin = new EntityCoordinates(component.PlanetMap.Value, Vector2.Zero);
        var directions = new Vector2i[]
        {
            ( 0,  1),
            ( 1,  1),
            ( 1,  0),
            ( 0, -1),
            (-1, -1),
            (-1,  0),
            ( 1, -1),
            (-1,  1),
        };

        var structuresToBuild = new List<DungeonConfigPrototype>();
        foreach (var (dungeon, count) in component.Destination.Structures)
        {
            var dungeonProto = _prototypeManager.Index<DungeonConfigPrototype>(dungeon);

            for (var i = 0; i < count; ++i)
            {
                structuresToBuild.Add(dungeonProto);
            }
        }

        _random.Shuffle(structuresToBuild);
        _random.Shuffle(directions);

        foreach (var direction in directions)
        {
            var minDistance = component.Destination.StructureDistance;
            var distance = _random.Next(minDistance, (int) (minDistance * 1.2));

            var point = direction * distance;

            var dungeonProto = structuresToBuild.Pop();
            var dungeon = await _dungeonSystem.GenerateDungeonAsync(dungeonProto, component.PlanetMap.Value, component.PlanetGrid,
                point, _random.Next());

            component.Structures.Add(dungeon);
        }
    }


    private MapId LoadDefaultMap()
    {
        _gameTicker.LoadGameMap(_prototypeManager.Index<GameMapPrototype>(DefaultShuttle), _gameTicker.DefaultMap, new MapLoadOptions(), null);
        return _gameTicker.DefaultMap;
    }

    [ValidatePrototypeId<GameMapPrototype>]
    private const string DefaultShuttle = "ShwrAdventurer";
    private bool AttachMap(EntityUid uid, ShipwreckedRuleComponent component)
    {
        var mapId = component.SpaceMapId ?? _gameTicker.DefaultMap;
        //var spaceMapUid = _mapManager.GetMapEntityId(_gameTicker.DefaultMap);

        var isValidMap = false;

        var query = EntityQueryEnumerator<SpawnPointComponent, MetaDataComponent, TransformComponent>();
        while (query.MoveNext(out var spawnPointComponent,out var meta, out var xform))
        {
            if (meta.EntityPrototype?.ID != component.SpawnPointHecate.Id)
                continue;

            if (xform.MapID != mapId)
                continue;

            isValidMap = true;
            break;
        }

        if (!isValidMap)
        {
            if (_mapManager.MapExists(_gameTicker.DefaultMap))
            {
                var qp = EntityQueryEnumerator<ActorComponent, MetaDataComponent, TransformComponent>();
                while (qp.MoveNext(out var actor, out _, out _))
                {
                    _gameTicker.Respawn(actor.PlayerSession);
                }
                _mapManager.DeleteMap(_gameTicker.DefaultMap);


                foreach (var map in _mapManager.GetAllMapIds().ToArray())
                {
                    _mapManager.DeleteMap(map);
                }

                _mapManager.CreateMap(_gameTicker.DefaultMap);
                if (_gameTicker.RunLevel != GameRunLevel.InRound)
                {
                    _mapManager.AddUninitializedMap(_gameTicker.DefaultMap);
                }

            }

            mapId = LoadDefaultMap();
        }

        var shuttleStation = _stationSystem.GetStationInMap(mapId); // then load secret no station in preloaded
        if (shuttleStation == null)
        {
            mapId = LoadDefaultMap();
            shuttleStation = _stationSystem.GetStationInMap(mapId);
        }
        component.SpaceMapId = mapId;
        component.ShuttleStation = shuttleStation;

        var shuttleGrid = GetShuttleInMap(mapId);//_stationSystem.GetStationInMap(_gameTicker.DefaultMap);
        if (shuttleGrid == null)
        {
            return false;
        }
        EnsureComp<PreventPilotComponent>(shuttleGrid.Value);
        component.Shuttle = shuttleGrid;

        return true;
    }

    private void SpawnHecate(ShipwreckedRuleComponent component)
    {
        if (component.Hecate != null)
        {
            _sawmill.Warning("Hecate was already spawned.");
            return;
        }

        var query = EntityQueryEnumerator<SpawnPointComponent, MetaDataComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var meta, out var xform))
        {
            if (meta.EntityPrototype?.ID != component.SpawnPointHecate.Id)
                continue;

            if (xform.GridUid != component.Shuttle)
                continue;

            component.Hecate = Spawn(component.HecatePrototype, xform.Coordinates);
            EnsureComp<TTSComponent>(component.Hecate.Value).VoicePrototypeId = component.Tts;

            if (TryComp<ShipwreckedNPCHecateComponent>(component.Hecate, out var hecateComponent))
                hecateComponent.Rule = component;

            _audioSystem.PlayPvs(new SoundPathSpecifier("/Audio/Nyanotrasen/Mobs/Hologram/hologram_start.ogg"), component.Hecate.Value);

            return;
        }

        throw new ArgumentException("Shipwrecked shuttle has no valid spawn points for Hecate.");
    }

    private List<EntityCoordinates> GetSpawnPoints(ShipwreckedRuleComponent component)
    {
        var spawns = new List<EntityCoordinates>();

        var query = EntityQueryEnumerator<SpawnPointComponent, MetaDataComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var meta, out var xform))
        {
            if (meta.EntityPrototype?.ID != component.SpawnPointTraveller.Id)
                continue;

            if (xform.GridUid != component.Shuttle)
                continue;

            spawns.Add(xform.Coordinates);
        }

        if (spawns.Count == 0)
            throw new ArgumentException("Shipwrecked shuttle has no valid spawn points for travellers.");

        return spawns;
    }

    private bool SpawnTraveller(ICommonSession player, EntityCoordinates spawnPoint, StringBuilder manifest, ShipwreckedRuleComponent component)
    {
        var profile = _preferencesManager.GetPreferences(player.UserId).SelectedCharacter as HumanoidCharacterProfile;

        if (profile == null)
        {
            // This player has no selected character profile.
            // Give them something random.

            // The following 3 lines are from SpawnPlayerMob because it depends on the randomize character CVar being set,
            // and it's easier to copy this than mess with the API.
            var weightId = _configurationManager.GetCVar(Shared.CCVar.CCVars.ICRandomSpeciesWeights);
            var weights = _prototypeManager.Index<WeightedRandomPrototype>(weightId);
            var speciesId = weights.Pick(_random);

            profile = HumanoidCharacterProfile.RandomWithSpecies(speciesId);
        }

        var jobProtoId = _random.Pick(component.AvailableJobPrototypes);

        if (!_prototypeManager.TryIndex(jobProtoId, out var jobPrototype))
            throw new ArgumentException($"Invalid JobPrototype: {jobProtoId}");

        var mindId = _mindSystem.CreateMind(player.UserId, profile.Name);

        var job = new JobComponent
        {
            Prototype = jobProtoId
        };
        _roleSystem.MindAddRole(mindId, job);

        var mob = _stationSpawningSystem.SpawnPlayerMob(spawnPoint, job, profile, station: null);
        var mobName = MetaData(mob).EntityName;

        manifest.AppendLine(Loc.GetString("passenger-manifest-passenger-line",
                ("name", mobName),
                ("details", jobPrototype.LocalizedName)));

        // SpawnPlayerMob requires a PDA to setup the ID details,
        // and PDAs are a bit too posh for our rugged travellers.
        if (_inventorySystem.TryGetSlotEntity(mob, "id", out var idUid) &&
            TryComp<IdCardComponent>(idUid, out var idCardComponent))
        {
            _cardSystem.TryChangeFullName(idUid.Value, mobName, idCardComponent);
            _cardSystem.TryChangeJobTitle(idUid.Value, jobPrototype.LocalizedName, idCardComponent);
        }

        EnsureComp<ZombieImmuneComponent>(mob);

        if (TryComp<BuckleComponent>(mob, out var buckle))
        {
            // Try to put them in a chair so they don't get knocked over by FTL at round-start...
            foreach (var nearbyEntity in _entityLookupSystem.GetEntitiesInRange(mob, 1f))
            {
                if (HasComp<StrapComponent>(nearbyEntity))
                {
                    _buckleSystem.TryBuckle(mob, mob, nearbyEntity, buckle);
                    break;
                }
            }
        }

        _mindSystem.TransferTo(mindId, mob);

        EnsureComp<ShipwreckSurvivorComponent>(mob);
        component.Survivors.Add((mob, player));

        var warpPoint = EnsureComp<WarpPointComponent>(mob);
        warpPoint.Location = mobName;
        /* warpPoint.Follow = true; */

        if (_autoPacified)
        {
            EnsureComp<PacifiedComponent>(mob);
        }

        return true;
    }

    private void DamageShuttleMidflight(ShipwreckedRuleComponent component)
    {
        if (component.Shuttle == null)
            return;

        // Damage vital pieces of the shuttle.
        //
        // * Console can go crunch when the ship smashes.
        // * Thrusters can be blown out safely.
        // * Generator will need to be replaced anyway as it's dying.
        //

        // Blow the thrusters.
        var query = EntityQueryEnumerator<ThrusterComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var thruster, out var xform))
        {
            if (xform.GridUid != component.Shuttle)
                continue;

            if (thruster.Type == ThrusterType.Angular)
                // Don't blow up the gyroscope.
                // It's the thruster that's inside.
                continue;

            // Keep track of how many thrusters we had.
            ++component.OriginalThrusterCount;

            // If these get destroyed at any point during the round, escape becomes impossible.
            // So make them indestructible.
            RemComp<DestructibleComponent>(uid);

            // Disallow them to be broken down, too.
            RemComp<ConstructionComponent>(uid);

            // These should be weak enough to rough up the walls but not destroy them.
            _explosionSystem.QueueExplosion(uid, "DemolitionCharge",
                2f,
                2f,
                2f,
                // Try not to break any tiles.
                tileBreakScale: 0,
                maxTileBreak: 0,
                canCreateVacuum: false,
                addLog: false);
        }

        // Ensure that all generators on the shuttle will decay.
        // Get the total power supply so we know how much to damage the generators by.
        /*
        var totalPowerSupply = 0f;
        var generatorQuery = EntityQueryEnumerator<PowerSupplierComponent, TransformComponent>();
        while (generatorQuery.MoveNext(out _, out var powerSupplier, out var xform))
        {
            if (xform.GridUid != component.Shuttle)
                continue;

            totalPowerSupply += powerSupplier.MaxSupply;
        }

        generatorQuery = EntityQueryEnumerator<PowerSupplierComponent, TransformComponent>();
        while (generatorQuery.MoveNext(out var uid, out var powerSupplier, out var xform))
        {
            if (xform.GridUid != component.Shuttle)
                continue;

            EnsureComp<FinitePowerSupplierComponent>(uid);

            // Hit it right away.
            powerSupplier.MaxSupply *= (component.OriginalPowerDemand / totalPowerSupply) * 0.96f;
        }
        */
    }

    public void MakeCrater(MapGridComponent grid, EntityCoordinates coordinates)
    {
        // Clear the area with a bomb.
        _explosionSystem.QueueExplosion(
            coordinates.ToMap(EntityManager, _transformSystem),
            "DemolitionCharge",
            200f,
            5f,
            30f,
            // Try not to break any tiles.
            // It's weird on planets.
            tileBreakScale: 0,
            maxTileBreak: 0,
            canCreateVacuum: false,
            addLog: false);

        // Put down a nice crater.
        var center = grid.GetTileRef(coordinates);
        var sand = (ContentTileDefinition) _tileDefinitionManager["FloorAsteroidCoarseSand0"];
        var crater = (ContentTileDefinition) _tileDefinitionManager["FloorAsteroidCoarseSandDug"];

        for (var y = -1; y <= 1; ++y)
        {
            for (var x = -1; x <= 1; ++x)
            {
                _tileSystem.ReplaceTile(grid.GetTileRef(center.GridIndices + new Vector2i(x, y)), sand);
            }
        }

        _tileSystem.ReplaceTile(center, crater);
    }

    private bool TryGetRandomStructureSpot(ShipwreckedRuleComponent component,
         out EntityCoordinates coordinates,
         [NotNullWhen(true)] out Dungeon? structure)
    {
        coordinates = EntityCoordinates.Invalid;
        structure = null;

        if (component.PlanetMap == null)
            throw new ArgumentException($"Shipwrecked failed to have a planet by the time a structure spot was needed.");

        if (component.Structures.Count == 0)
        {
            _sawmill.Warning("Unable to get a structure spot. Making something up...");

            var distance = component.Destination?.StructureDistance ?? 50;
            coordinates = new EntityCoordinates(component.PlanetMap.Value, _random.NextVector2(-distance, distance));
            return false;
        }

        // From a gameplay perspective, it would be most useful to place
        // the vital pieces around the structures, that way players aren't
        // wandering around the entire map looking for them.
        //
        // Some biomes also generate walls which could hide them.
        // At least these structures are large enough to act as a beacon
        // of sorts.

        structure = _random.Pick(component.Structures);
        var offset = _random.NextVector2(-13, 13);
        var xy = _random.Pick(structure.Rooms).Center + offset;

        coordinates = new EntityCoordinates(component.PlanetMap.Value, xy);

        return true;
    }

    private void PrepareVitalShuttlePieces(ShipwreckedRuleComponent component)
    {
        if (component.PlanetMap == null || component.PlanetGrid == null)
            return;

        var thrusterQuery = EntityQueryEnumerator<ThrusterComponent, TransformComponent>();
        while (thrusterQuery.MoveNext(out var uid, out var thruster, out var xform))
        {
            if (xform.GridUid != component.Shuttle)
                continue;

            if (thruster.Type == ThrusterType.Angular)
                // Ignore the gyroscope.
                continue;

            if (TryGetRandomStructureSpot(component, out var spot, out var structure))
            {
                if (component.VitalPieceStructureSpots.TryGetValue(structure, out var spots))
                    spots.Add(spot);
                else
                    component.VitalPieceStructureSpots.Add(structure, new () {spot});
            }

            _sawmill.Info($"Space debris! {ToPrettyString(uid)} will go to {spot}");

            // We do this before moving the pieces,
            // so they don't get affected by the explosion.
            MakeCrater(component.PlanetGrid, spot);

            component.VitalPieces.Add(uid, (spot, structure));
        }

        // Part of the escape objective requires the shuttle to have enough
        // power for liftoff, but due to luck of the draw with dungeon generation,
        // it's possible that not enough generators are spawned in.
        var planetGeneratorCount = 0;
        var planetGeneratorPower = 0f;
        var generatorQuery = EntityQueryEnumerator<PowerSupplierComponent, TransformComponent>();
        while (generatorQuery.MoveNext(out _, out var powerSupplier, out var xform))
        {
            if (xform.GridUid != component.PlanetMap)
                continue;

            planetGeneratorPower += powerSupplier.MaxSupply;
            ++planetGeneratorCount;
        }

        _sawmill.Info($"Shipwreck destination has {planetGeneratorPower} W worth of {planetGeneratorCount} scavengeable generators.");

        if (planetGeneratorPower < component.OriginalPowerDemand)
        {
            // It's impossible to find enough generators to supply the shuttle's
            // original power demand, assuming the players let the generator
            // completely fail, therefore, we must spawn some generators,
            // Deus Ex Machina.

            // This is all very cheesy that there would be generators just lying around,
            // but I'd rather players be able to win than be hard-locked into losing.

            // How many will we need?
            const float UraniumPower = 15000f;
            var generatorsNeeded = Math.Max(1, component.OriginalPowerDemand / UraniumPower);

            for (int i = 0; i < generatorsNeeded; ++i)
            {
                // Just need a temporary spawn point away from everything.
                var somewhere = new EntityCoordinates(component.PlanetMap.Value, 200f + i, 200f + i);
                var uid = Spawn("PortableGeneratorSuperPacman", somewhere);

                TryGetRandomStructureSpot(component, out var spot, out var structure);
                _sawmill.Info($"Heaven generator! {ToPrettyString(uid)} will go to {spot}");

                MakeCrater(component.PlanetGrid, spot);
                component.VitalPieces.Add(uid, (spot, structure));
            }
        }
    }

    private void DecoupleShuttleEngine(ShipwreckedRuleComponent component)
    {
        if (component.Shuttle == null)
            return;

        // Stop thrusters from burning anyone when re-anchored.
        _thrusterSystem.DisableLinearThrusters(Comp<ShuttleComponent>(component.Shuttle.Value));

        // Move the vital pieces of the shuttle down to the planet.
        foreach (var (uid, (destination, _)) in component.VitalPieces)
        {
            var warpPoint = EnsureComp<WarpPointComponent>(uid);
            warpPoint.Location = Loc.GetString("shipwrecked-warp-point-vital-piece");

            _transformSystem.SetCoordinates(uid, destination);
        }

        if (component.Shuttle == null)
            return;

        // Spawn scrap in front of the shuttle's window.
        // It'll look cool.
        var shuttleXform = Transform(component.Shuttle.Value);
        var spot = shuttleXform.MapPosition.Offset(-3, 60);

        for (var i = 0; i < 9; ++i)
        {
            var scrap = Spawn("SheetSteel1", spot.Offset(_random.NextVector2(-4, 3)));
            Transform(scrap).LocalRotation = _random.NextAngle();
        }
    }

    private void SpawnFactionMobs(ShipwreckedRuleComponent component, IEnumerable<EntitySpawnEntry> entries, DungeonRoom room)
    {
        if (component.PlanetGrid == null)
            return;

        // Some more code adapted from salvage missions.
        var spawns = EntitySpawnCollection.GetSpawns(entries, _random);

        foreach (var entry in spawns)
        {
            var spawnTile = room.Tiles.ElementAt(_random.Next(room.Tiles.Count));
            var spawnPosition = component.PlanetGrid.GridTileToLocal(spawnTile);

            var uid = EntityManager.CreateEntityUninitialized(entry, spawnPosition);
            RemComp<GhostTakeoverAvailableComponent>(uid);
            RemComp<GhostRoleComponent>(uid);
            EntityManager.InitializeAndStartEntity(uid);
        }
    }

    private void SpawnFaction(ShipwreckedRuleComponent component, string id, IEnumerable<Dungeon> structures)
    {
        if (!_prototypeManager.TryIndex<ShipwreckFactionPrototype>(id, out var faction) ||
            component.PlanetGrid == null)
        {
            return;
        }

        // Some more pseudo-copied code from salvage missions, simplified.
        foreach (var structure in structures)
        {
            // Spawn an objective defender if there's a vital piece here.
            if (faction.ObjectiveDefender != null &&
                component.VitalPieceStructureSpots.TryGetValue(structure, out var spots) &&
                spots.Count > 0)
            {
                var spot = spots.Pop().Offset(_random.NextVector2(-2, 2));
                Spawn(faction.ObjectiveDefender, spot);
            }

            foreach (var room in structure.Rooms)
            {
                SpawnFactionMobs(component, faction.Active, room);
                SpawnFactionMobs(component, faction.Inactive, room);
            }
        }
    }

    private void SpawnFactions(ShipwreckedRuleComponent component)
    {
        if (component.PlanetMap == null ||
            component.PlanetGrid == null ||
            component.Destination == null)
        {
            return;
        }

        var availableFactions = component.Destination.Factions.ToList();
        if (availableFactions.Count == 0)
            return;

        _random.Shuffle(availableFactions);

        var availableStructures = component.Structures.ToList();
        if (availableStructures.Count == 0)
        {
            _sawmill.Error("NYI: There are no structures for the factions to spawn around.");
            return;
        }

        // For gameplay reasons, the factions will congregate around the vital shuttle pieces.
        // We can throw a few around the structures and in the wilderness.
        _random.Shuffle(availableStructures);

        if (availableFactions.Count == 1)
        {
            SpawnFaction(component, availableFactions.First(), availableStructures);
        }
        else
        {
            var split = _random.Next((int) (availableStructures.Count * 0.75), availableStructures.Count - 1);

            // Pick one faction to be the major power.
            var majorPower = availableFactions.Pop();
            var majorStructures = availableStructures.GetRange(0, split);

            SpawnFaction(component, majorPower, majorStructures);

            _sawmill.Info($"{majorPower} has taken control of {majorStructures.Count} structures.");

            // Pick another, different faction to be the minor power.
            var minorPower = availableFactions.Pop();
            var minorStructures = availableStructures.GetRange(split, availableStructures.Count - majorStructures.Count);

            SpawnFaction(component, minorPower, minorStructures);

            _sawmill.Info($"{minorPower} has taken control of {minorStructures.Count} structures.");
        }
    }

    private void CrashShuttle(EntityUid uid, ShipwreckedRuleComponent component)
    {
        if (component.Shuttle == null)
            return;

        if (!TryComp<MapGridComponent>(component.Shuttle, out var grid))
            return;

        // Slam the front window.
        var aabb = grid.LocalAABB;
        var topY = grid.LocalAABB.Top + 1;
        var bottomY = grid.LocalAABB.Bottom - 1;
        var centeredX = grid.LocalAABB.Width / 2 + aabb.Left;

        var xform = Transform(component.Shuttle.Value);
        var mapPos = xform.MapPosition;
        var smokeSpots = new List<MapCoordinates>();
        var front = mapPos.Offset(new Vector2(centeredX, topY));
        smokeSpots.Add(front);
        smokeSpots.Add(mapPos.Offset(new Vector2(centeredX, bottomY)));

        _explosionSystem.QueueExplosion(front, "Minibomb",
            200f,
            1f,
            100f,
            // Try not to break any tiles.
            tileBreakScale: 0,
            maxTileBreak: 0,
            canCreateVacuum: false,
            addLog: false);

        // Send up smoke and dust plumes.
        foreach (var spot in smokeSpots)
        {
            var smokeEnt = Spawn("Smoke", spot);
            var smoke = EnsureComp<SmokeComponent>(smokeEnt);

            // Breathing smoke is not good for you.
            var toxin = new Solution("Toxin", FixedPoint2.New(2));
            _smokeSystem.StartSmoke(smokeEnt, toxin, 20f, 70, smoke);
        }

        // Fry the console.
        var consoleQuery = EntityQueryEnumerator<TransformComponent, ShuttleConsoleComponent>();
        while (consoleQuery.MoveNext(out var consoleUid, out var consoleXform, out _))
        {
            if (consoleXform.GridUid != component.Shuttle)
                continue;

            var limit = _destructibleSystem.DestroyedAt(consoleUid);

            // Here at Nyanotrasen, we have damage variance, so...
            var damageVariance = _configurationManager.GetCVar(CCVars.DamageVariance);
            limit *= 1f + damageVariance;

            var smash = new DamageSpecifier();
            smash.DamageDict.Add("Structural", limit);
            _damageableSystem.TryChangeDamage(consoleUid, smash, ignoreResistances: true);

            // Break, because we're technically modifying the enumeration by destroying the console.
            break;
        }

        var crashSound = new SoundPathSpecifier("/Audio/Nyanotrasen/Effects/crash_impact_metal.ogg");
        _audioSystem.PlayPvs(crashSound, component.Shuttle.Value);
    }

    private void DispatchShuttleAnnouncement(string message, ShipwreckedRuleComponent component)
    {
        var wrappedMessage = Loc.GetString("shipwrecked-shuttle-announcement",
            ("sender", "Hecate"),
            ("message", FormattedMessage.EscapeText(message)));

        var ghostQuery = GetEntityQuery<GhostComponent>();
        var xformQuery = GetEntityQuery<TransformComponent>();
        var filter = Filter.Empty();

        foreach (var player in _playerManager.Sessions)
        {
            if (player.AttachedEntity is not {Valid: true} playerEntity)
                continue;

            if (ghostQuery.HasComponent(playerEntity))
            {
                // Add ghosts.
                filter.AddPlayer(player);
                continue;
            }

            var xform = xformQuery.GetComponent(playerEntity);
            if (xform.GridUid != component.Shuttle)
                continue;

            // Add entities inside the shuttle.
            filter.AddPlayer(player);
        }

        _chatManager.ChatMessageToManyFiltered(filter,
            ChatChannel.Radio,
            message,
            wrappedMessage,
            component.Shuttle.GetValueOrDefault(),
            false,
            true,
            Color.SeaGreen);

        var announcementSound = new SoundPathSpecifier(ChatSystem.DefaultAnnouncementSound);
        var announcementEv = new AnnouncementSpokeEvent(filter, _audioSystem.GetSound(announcementSound), AudioParams.Default.WithVolume(-2f), message);
        RaiseLocalEvent(announcementEv);
        //var audioPath = _audioSystem.GetSound(audio);
        //_audioSystem.PlayGlobal(audioPath, filter, true, AudioParams.Default.WithVolume(1f));
    }

    private void HecateSay(string message, ShipwreckedRuleComponent component)
    {
        if (component.Hecate is not {} hecate)
        {
            _sawmill.Warning($"Hecate was not found for message: {message}");
            return;
        }

        _npcConversationSystem.QueueResponse(hecate, new NPCResponse(message));
    }

    protected override void ActiveTick(EntityUid uid, ShipwreckedRuleComponent component, GameRuleComponent gameRule, float frameTime)
    {
        base.ActiveTick(uid, component, gameRule, frameTime);

        var curTime = _gameTiming.CurTime;

        if (component.EventSchedule.Count > 0 && curTime >= component.NextEventTick)
        {
            // Pop the event.
            var curEvent = component.EventSchedule[0];
            component.EventSchedule.RemoveAt(0);

            // Add the next event's offset to the ticker.
            if (component.EventSchedule.Count > 0)
                component.NextEventTick = curTime + component.EventSchedule[0].timeOffset;

            _sawmill.Info($"Running event: {curEvent}");

            switch (curEvent.eventId)
            {
                case ShipwreckedEventId.AnnounceTransit:
                {
                    // We have to wait for the dungeon atlases to be ready, so do this here.
                    SpawnPlanetaryStructures(uid, component);

                    DispatchShuttleAnnouncement(Loc.GetString("shipwrecked-hecate-shuttle-in-transit"),
                        component);
                    break;
                }
                case ShipwreckedEventId.ShowHecate:
                {
                    SpawnHecate(component);
                    break;
                }
                case ShipwreckedEventId.IntroduceHecate:
                {
                    HecateSay(Loc.GetString("hecate-qa-user-interface"),
                        component);
                    break;
                }
                case ShipwreckedEventId.EncounterTurbulence:
                {
                    DispatchShuttleAnnouncement(Loc.GetString("shipwrecked-hecate-shuttle-turbulence-nebula"),
                        component);
                    break;
                }
                case ShipwreckedEventId.ShiftParallax:
                {
                    var spaceMapId = Transform(component.Shuttle!.Value).MapID;

                    var spaceMap = _mapManager.GetMapEntityId(spaceMapId);
                    var parallax = EnsureComp<ParallaxComponent>(spaceMap);
                    parallax.Parallax = "ShipwreckedTurbulence1";
                    Dirty(spaceMap, parallax);
                    break;
                }
                case ShipwreckedEventId.MidflightDamage:
                {
                    DamageShuttleMidflight(component);

                    if (component.Hecate != null)
                    {
                        _npcConversationSystem.EnableConversation(component.Hecate.Value, false);
                        _npcConversationSystem.EnableIdleChat(component.Hecate.Value, false);
                    }

                    break;
                }
                case ShipwreckedEventId.Alert:
                {
                    PrepareVitalShuttlePieces(component);
                    HecateSay(Loc.GetString("shipwrecked-hecate-report-alert"),
                        component);
                    break;
                }
                case ShipwreckedEventId.DecoupleEngine:
                {
                    DecoupleShuttleEngine(component);
                    HecateSay(Loc.GetString("shipwrecked-hecate-report-decouple-engine"),
                        component);
                    break;
                }
                case ShipwreckedEventId.SendDistressSignal:
                {
                    SpawnFactions(component);
                    DispatchShuttleAnnouncement(Loc.GetString("shipwrecked-hecate-shuttle-distress-signal"),
                        component);
                    break;
                }
                case ShipwreckedEventId.InterstellarBody:
                {
                    HecateSay(Loc.GetString("shipwrecked-hecate-report-interstellar-body"),
                        component);
                    break;
                }
                case ShipwreckedEventId.EnteringAtmosphere:
                {
                    HecateSay(Loc.GetString("shipwrecked-hecate-report-entering-atmosphere"),
                        component);
                    break;
                }
                case ShipwreckedEventId.Crash:
                {
                    CrashShuttle(uid, component);
                    break;
                }
                case ShipwreckedEventId.AfterCrash:
                {
                    DispatchShuttleAnnouncement(Loc.GetString("shipwrecked-hecate-shuttle-crashed"),
                        component);
                    break;
                }
                case ShipwreckedEventId.Sitrep:
                {
                    HecateSay(Loc.GetString("shipwrecked-hecate-aftercrash-sitrep"),
                        component);

                    if (component.Hecate == null)
                        break;

                    _npcConversationSystem.EnableConversation(component.Hecate.Value);
                    _npcConversationSystem.UnlockDialogue(component.Hecate.Value,
                        new HashSet<string>()
                        {
                            "generator",
                            "rescue",
                            "scans",
                            "status",
                            "weapons"
                        });
                    CloseAllRuleJobs(component);
                    foreach (var (mob, _) in component.Survivors)
                    {
                        RemCompDeferred<PacifiedComponent>(mob);
                    }

                    break;
                }
                case ShipwreckedEventId.Launch:
                {
                    if (component.Shuttle == null || component.SpaceMapId == null)
                        break;

                    var shuttle = component.Shuttle.Value;
                    var spaceMap = _mapManager.GetMapEntityId(component.SpaceMapId.Value);

                    var query = EntityQueryEnumerator<TransformComponent, ActorComponent>();
                    while (query.MoveNext(out var actorUid, out var xform, out _))
                    {
                        if (xform.GridUid == component.Shuttle)
                            continue;

                        _popupSystem.PopupEntity(Loc.GetString("shipwrecked-shuttle-popup-left-behind"),
                            actorUid, actorUid, PopupType.Large);
                    }

                    HecateSay(Loc.GetString("shipwrecked-hecate-launch"),
                        component);

                    _shuttleSystem.FTLTravel(shuttle,
                        Comp<ShuttleComponent>(shuttle),
                        new EntityCoordinates(spaceMap, 0, 0),
                        hyperspaceTime: 120f);
                    break;
                }
            }
        }
    }

    private EntityUid? GetShuttleInMap(MapId map)
    {
        return EntityQuery<BecomesStationComponent, ShuttleComponent, TransformComponent>(true)
            .FirstOrNull(x=>x.Item3.MapID == map)?.Item3.GridUid;
    }

    private bool _autoPacified = true;

    private void OpenAllRuleJobs(ShipwreckedRuleComponent component)
    {
        /*
        if (component.ShuttleStation == null)
        {
            return;
        }

        foreach (var componentAvailableJobPrototype in component.AvailableJobPrototypes)
        {
            _stationJobsSystem.TryAdjustJobSlot(component.ShuttleStation.Value, componentAvailableJobPrototype.Id, 0,
                true, true);
            _stationJobsSystem.MakeJobUnlimited(component.ShuttleStation.Value, componentAvailableJobPrototype.Id);
        }
        */
        _autoPacified = true;
    }

    private void CloseAllRuleJobs(ShipwreckedRuleComponent component)
    {
        if (component.ShuttleStation == null)
        {
            return;
        }

        foreach (var componentAvailableJobPrototype in component.AvailableJobPrototypes)
        {
            _stationJobsSystem.TrySetJobSlot(component.ShuttleStation.Value, componentAvailableJobPrototype.Id, 0, true);
        }

        _autoPacified = false;
    }

    protected override void Added(EntityUid uid, ShipwreckedRuleComponent component, GameRuleComponent gameRule, GameRuleAddedEvent args)
    {
        base.Added(uid, component, gameRule, args);

        var destination = _random.Pick(component.ShipwreckDestinationPrototypes);

        component.Destination = _prototypeManager.Index<ShipwreckDestinationPrototype>(destination);


        if (_gameTicker.RunLevel == GameRunLevel.InRound)
        {
            AttachMap(uid, component);
            SpawnPlanet(uid, component);

            foreach (var map in _mapManager.GetAllMapIds().ToArray())
            {
                if (component.PlanetMapId.HasValue && component.PlanetMapId.Value == map)
                {
                    continue;
                }

                if (_gameTicker.DefaultMap == map)
                {
                    continue;
                }

                //cleanup
                _mapManager.DeleteMap(map);
            }

            if (component.Shuttle == null)
                throw new ArgumentException($"Shipwrecked failed to spawn a Shuttle.");
        }

        // Currently, the AutoCallStartTime is part of the public API and not access restricted.
        // If this ever changes, I will send a patch upstream to allow it to be altered.
        _roundEndSystem.AutoCallStartTime = TimeSpan.MaxValue;
    }

    protected override void Started(EntityUid uid, ShipwreckedRuleComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        AttachMap(uid, component);
        SpawnPlanet(uid, component);

        if (component.PlanetMapId != null && _mapManager.MapExists(component.PlanetMapId.Value) && _mapManager.IsMapPaused(component.PlanetMapId.Value))
        {
            _mapManager.SetMapPaused(component.PlanetMapId!.Value, false);
        }

        var loadQuery = EntityQueryEnumerator<ApcPowerReceiverComponent, TransformComponent>();
        while (loadQuery.MoveNext(out _, out var apcPowerReceiver, out var xform))
        {
            if (xform.GridUid != component.Shuttle)
                continue;

            component.OriginalPowerDemand += apcPowerReceiver.Load;
        }

        _sawmill.Info($"The original power demand for the shuttle is {component.OriginalPowerDemand} W");

        var shuttle = component.Shuttle!.Value;

        // Do some quick math to figure out at which point the FTL should end.
        // Do this when the rule starts and not when it's added so the timing is correct.
        var flightTime = TimeSpan.Zero;
        foreach (var item in component.EventSchedule)
        {
            flightTime += item.timeOffset;

            if (item.eventId == ShipwreckedEventId.Crash)
                break;
        }

        // Tiny adjustment back in time so Crash runs just after FTL ends.
        flightTime -= TimeSpan.FromMilliseconds(10);

        component.NextEventTick = _gameTiming.CurTime + component.EventSchedule[0].timeOffset;

        _shuttleSystem.FTLTravel(shuttle,
            Comp<ShuttleComponent>(shuttle),
            Transform(component.PlanetMap.GetValueOrDefault()).Coordinates,
            // The travellers are already in FTL by the time the gamemode starts.
            startupTime: 0,
            hyperspaceTime: (float) flightTime.TotalSeconds);

        OpenAllRuleJobs(component);
    }

    private EntityUid? SpawnManifest(EntityUid uid, ShipwreckedRuleComponent component)
    {
        var consoleQuery = EntityQueryEnumerator<TransformComponent, ShuttleConsoleComponent>();
        while (consoleQuery.MoveNext(out var consoleUid, out var consoleXform, out _))
        {
            if (consoleXform.GridUid != component.Shuttle)
                continue;

            return Spawn("PaperManifestPassenger", consoleXform.Coordinates);
        }

        return null;
    }

    private void OnPlayersSpawning(RulePlayerSpawningEvent ev)
    {
        var query = EntityQueryEnumerator<ShipwreckedRuleComponent, GameRuleComponent>();
        while (query.MoveNext(out var uid, out var shipwrecked, out var gameRule))
        {
            if (!GameTicker.IsGameRuleAdded(uid, gameRule))
                continue;

            var players = new List<ICommonSession>(ev.PlayerPool)
                .Where(player => ev.Profiles.ContainsKey(player.UserId));

            var manifest = SpawnManifest(uid, shipwrecked);
            var manifestText = new StringBuilder();

            var spawnPoints = GetSpawnPoints(shipwrecked);
            _random.Shuffle(spawnPoints);

            var lastSpawnPointUsed = 0;
            foreach (var player in players)
            {
                SpawnTraveller(player, spawnPoints[lastSpawnPointUsed++], manifestText, shipwrecked);
                lastSpawnPointUsed = lastSpawnPointUsed % spawnPoints.Count;

                ev.PlayerPool.Remove(player);
                GameTicker.PlayerJoinGame(player);
            }

            manifestText.AppendLine(Loc.GetString("passenger-manifest-end-line"));

            if (manifest != null)
                _paperSystem.SetContent(manifest.Value, manifestText.ToString());
        }
    }

    private void OnRoundEndText(RoundEndTextAppendEvent ev)
    {
        var query = EntityQueryEnumerator<ShipwreckedRuleComponent, GameRuleComponent>();
        while (query.MoveNext(out var uid, out var shipwrecked, out var gameRule))
        {
            if (!GameTicker.IsGameRuleActive(uid, gameRule))
                continue;

            ev.AddLine(Loc.GetString("shipwrecked-list-start"));

            foreach (var (survivor, session) in shipwrecked.Survivors)
            {
                var name = Loc.GetString("generic-unknown");
                if (!TerminatingOrDeleted(survivor))
                {
                    name = MetaData(survivor).EntityName;
                }
                if (IsDead(survivor))
                {
                    ev.AddLine(Loc.GetString("shipwrecked-list-perished-name",
                        ("name", name),
                        ("user", session.Name)));
                }
                else if (shipwrecked.AllObjectivesComplete &&
                    Transform(survivor).GridUid == shipwrecked.Shuttle)
                {
                    ev.AddLine(Loc.GetString("shipwrecked-list-escaped-name",
                        ("name", name),
                        ("user", session.Name)));
                }
                else
                {
                    ev.AddLine(Loc.GetString("shipwrecked-list-survived-name",
                        ("name", name),
                        ("user", session.Name)));
                }
            }

            ev.AddLine("");
            ev.AddLine(Loc.GetString("shipwrecked-list-start-objectives"));

            if (GetLaunchConditionConsole(shipwrecked))
                ev.AddLine(Loc.GetString("shipwrecked-list-objective-console-pass"));
            else
                ev.AddLine(Loc.GetString("shipwrecked-list-objective-console-fail"));

            if (GetLaunchConditionGenerator(shipwrecked))
                ev.AddLine(Loc.GetString("shipwrecked-list-objective-generator-pass"));
            else
                ev.AddLine(Loc.GetString("shipwrecked-list-objective-generator-fail"));

            if (GetLaunchConditionThrusters(shipwrecked, out var goodThrusters))
            {
                ev.AddLine(Loc.GetString("shipwrecked-list-objective-thrusters-pass",
                        ("totalThrusterCount", shipwrecked.OriginalThrusterCount)));
            }
            else if(goodThrusters == 0)
            {
                ev.AddLine(Loc.GetString("shipwrecked-list-objective-thrusters-fail",
                        ("totalThrusterCount", shipwrecked.OriginalThrusterCount)));
            }
            else
            {
                ev.AddLine(Loc.GetString("shipwrecked-list-objective-thrusters-partial",
                        ("goodThrusterCount", shipwrecked.OriginalThrusterCount),
                        ("totalThrusterCount", shipwrecked.OriginalThrusterCount)));
            }

            if (shipwrecked.AllObjectivesComplete)
            {
                ev.AddLine("");
                ev.AddLine(Loc.GetString("shipwrecked-list-all-objectives-complete"));
            }
        }
    }

    private void OnSurvivorMobStateChanged(EntityUid survivor, ShipwreckSurvivorComponent component, MobStateChangedEvent args)
    {
        var query = EntityQueryEnumerator<ShipwreckedRuleComponent, GameRuleComponent>();
        while (query.MoveNext(out var uid, out var shipwrecked, out var gameRule))
        {
            if (!GameTicker.IsGameRuleActive(uid, gameRule))
                continue;

            CheckShouldRoundEnd(uid, shipwrecked);
        }
    }

    private void OnSurvivorBeingGibbed(EntityUid survivor, ShipwreckSurvivorComponent component, BeingGibbedEvent args)
    {
        var query = EntityQueryEnumerator<ShipwreckedRuleComponent, GameRuleComponent>();
        while (query.MoveNext(out var uid, out var shipwrecked, out var gameRule))
        {
            if (!GameTicker.IsGameRuleActive(uid, gameRule))
                continue;

            CheckShouldRoundEnd(uid, shipwrecked);
        }
    }

    private void OnZombified(ref EntityZombifiedEvent args)
    {
        var query = EntityQueryEnumerator<ShipwreckedRuleComponent, GameRuleComponent>();
        while (query.MoveNext(out var uid, out var shipwrecked, out var gameRule))
        {
            if (!GameTicker.IsGameRuleActive(uid, gameRule))
                continue;

            CheckShouldRoundEnd(uid, shipwrecked);
        }
    }

    // This should probably be something general, but I'm not sure where to put it,
    // and it's small enough to stay here for now. Feel free to move it.
    public bool IsDead(EntityUid uid)
    {
        return (_mobStateSystem.IsDead(uid) ||
            // Zombies are not dead-dead, so check for that.
            HasComp<ZombieComponent>(uid) ||
            Deleted(uid));
    }

    private void CheckShouldRoundEnd(EntityUid uid, ShipwreckedRuleComponent component)
    {
        var totalSurvivors = component.Survivors.Count;
        var deadSurvivors = 0;

        var zombieQuery = GetEntityQuery<ZombieComponent>();

        foreach (var (survivor, _) in component.Survivors)
        {
            // Check if everyone's dead.
            if (IsDead(survivor))
                ++deadSurvivors;
        }

        if (deadSurvivors == totalSurvivors)
            _roundEndSystem.EndRound();
    }

# region Hecate Dynamic Responses

    private void OnInitHecate(EntityUid uid, ShipwreckedNPCHecateComponent component, MapInitEvent args)
    {
        component.GunSafe.Clear();
        component.EngineBayDoor.Clear();

        var doorQuery = GetEntityQuery<DoorComponent>();
        var storageQuery = GetEntityQuery<EntityStorageComponent>();

        var query = EntityQueryEnumerator<AccessReaderComponent, TransformComponent>();
        while (query.MoveNext(out var entity, out var access, out var xform))
        {
            if (xform.GridUid != Transform(uid).GridUid)
                continue;

            foreach (var accessList in access.AccessLists)
            {
                if (accessList.Contains("Armory") && (storageQuery.HasComponent(entity) || doorQuery.HasComponent(entity)))
                {
                    // This is probably the gun safe.
                    component.GunSafe.Add(entity);
                }
                if (accessList.Contains("Engineering") && (storageQuery.HasComponent(entity) || doorQuery.HasComponent(entity)))
                {
                    // This is probably the engine bay door.
                    component.EngineBayDoor.Add(entity);
                }
            }
        }
    }

    private void OnAskGeneratorUnlock(EntityUid uid, ShipwreckedNPCHecateComponent component, ShipwreckedHecateAskGeneratorUnlockEvent args)
    {
        if (component.UnlockedEngineBay || !component.EngineBayDoor.Any())
            return;

        component.UnlockedEngineBay = true;

        foreach (var row in component.EngineBayDoor)
        {
            if (TerminatingOrDeleted(row))
                continue;
            if (TryComp<AirlockComponent>(row, out var airlock) && !airlock.EmergencyAccess)
            {
                _airlockSystem.ToggleEmergencyAccess(row, airlock);
                continue;
            }
            _lockSystem.Unlock(row, uid);
        }

        _npcConversationSystem.QueueResponse(uid, args.AccessGranted);
    }

    private void OnAskWeapons(EntityUid uid, ShipwreckedNPCHecateComponent component, ShipwreckedHecateAskWeaponsEvent args)
    {
        var response = component.UnlockedSafe ? args.AfterUnlock : args.BeforeUnlock;

        // Set the flag now so we don't get multiple unlock responses queued.
        component.UnlockedSafe = true;

        _npcConversationSystem.QueueResponse(uid, response);
    }

    private void OnAskWeaponsUnlock(EntityUid uid, ShipwreckedNPCHecateComponent component, ShipwreckedHecateAskWeaponsUnlockEvent args)
    {

        if (!component.GunSafe.Any())
        {
            _sawmill.Warning($"Hecate tried to unlock the gun safe, but it's missing.");
            return;
        }

        foreach (var row in component.GunSafe)
        {
            if (TerminatingOrDeleted(row) )
            {
                continue;
            }
            if (TryComp<AirlockComponent>(row, out var airlock) && !airlock.EmergencyAccess)
            {
                _airlockSystem.ToggleEmergencyAccess(row, airlock);
                continue;
            }
            _lockSystem.Unlock(row, uid);
        }
    }

    private bool GetLaunchConditionConsole(ShipwreckedRuleComponent component)
    {
        var consoleQuery = EntityQueryEnumerator<TransformComponent, ShuttleConsoleComponent>();
        while (consoleQuery.MoveNext(out var uid, out var xform, out _))
        {
            if (xform.GridUid != component.Shuttle)
                continue;

            // Just having it is good enough.
            return true;
        }

        return false;
    }

    private bool GetLaunchConditionGenerator(ShipwreckedRuleComponent component)
    {
        var totalSupply = 0f;

        var generatorQuery = EntityQueryEnumerator<PowerSupplierComponent, TransformComponent>();
        while (generatorQuery.MoveNext(out var uid, out var powerSupplier, out var xform))
        {
            if (xform.GridUid != component.Shuttle)
                continue;

            if (!xform.Anchored || !powerSupplier.Enabled)
                continue;

            // It should be good enough that we have the generator here and anchored.
            // There's not a significant need to see if it's wired in specifically to the engine.
            totalSupply += powerSupplier.MaxSupply;
        }

        return totalSupply >= component.OriginalPowerDemand;
    }

    private bool GetLaunchConditionThrusters(ShipwreckedRuleComponent component, out int goodThrusters)
    {
        goodThrusters = 0;

        var query = EntityQueryEnumerator<ThrusterComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var thruster, out var xform))
        {
            if (xform.GridUid != component.Shuttle)
                continue;

            if (thruster.Type == ThrusterType.Angular)
                // Skip the gyroscope.
                continue;

            if (!_thrusterSystem.CanEnable(uid, thruster))
                continue;

            ++goodThrusters;
        }

        return goodThrusters >= component.OriginalThrusterCount;
    }

    /// <summary>
    /// Queues responses for Hecate to give to the player if there's a failed condition
    /// </summary>
    /// <returns>Returns true if the shuttle can launch.</returns>
    private bool CheckAndReportLaunchConditionStatus(EntityUid uid, ShipwreckedNPCHecateComponent component, ShipwreckedHecateAskStatusOrLaunchEvent args)
    {
        var rule = component.Rule;
        if (rule == null)
            return false;

        var conditions = new (bool, NPCResponse)[] {
            (GetLaunchConditionConsole(rule), args.NeedConsole),
            (GetLaunchConditionGenerator(rule), args.NeedGenerator),
            (GetLaunchConditionThrusters(rule, out _), args.NeedThrusters),
        };

        foreach (var (status, response) in conditions)
        {
            if (!status)
                _npcConversationSystem.QueueResponse(uid, response);
        }

        var hasFailedCondition = conditions.Any(condition => condition.Item1 == false);
        return !hasFailedCondition;
    }

    private void OnAskStatus(EntityUid uid, ShipwreckedNPCHecateComponent component, ShipwreckedHecateAskStatusEvent args)
    {
        var rule = component.Rule;
        if (rule == null)
            return;

        if (!CheckAndReportLaunchConditionStatus(uid, component, args))
            return;

        if (_npcConversationSystem.IsDialogueLocked(uid, "launch"))
        {
            _npcConversationSystem.UnlockDialogue(uid, "launch");
            _npcConversationSystem.QueueResponse(uid, args.AllGreenFirst);
        }
        else
        {
            _npcConversationSystem.QueueResponse(uid, args.AllGreenAgain);
        }
    }

    private void OnAskLaunch(EntityUid uid, ShipwreckedNPCHecateComponent component, ShipwreckedHecateAskLaunchEvent args)
    {
        var rule = component.Rule;
        if (rule == null)
            return;

        if (component.Launching)
            return;

        if (!CheckAndReportLaunchConditionStatus(uid, component, args))
            // You know someone's going to try unanchoring a thruster after
            // getting all green but before launching.
            return;

        component.Launching = true;
        rule.AllObjectivesComplete = true;

        DispatchShuttleAnnouncement(Loc.GetString("shipwrecked-hecate-shuttle-prepare-for-launch"),
            rule);

        rule.NextEventTick = _gameTiming.CurTime + TimeSpan.FromMinutes(2);
        rule.EventSchedule.Add((TimeSpan.Zero, ShipwreckedEventId.Launch));

        var query = EntityQueryEnumerator<TransformComponent, ActorComponent>();
        while (query.MoveNext(out var actorUid, out var xform, out _))
        {
            if (xform.GridUid == rule.Shuttle)
                continue;

            _popupSystem.PopupEntity(Loc.GetString("shipwrecked-shuttle-popup-preparing"),
                actorUid, actorUid, PopupType.Large);
        }

        _npcConversationSystem.EnableConversation(uid, false);
    }

#endregion

}
