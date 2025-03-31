﻿using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Numerics;
using Content.Server._Lavaland.Biome;
using Content.Server._Lavaland.Procedural.Components;
using Content.Server.Atmos.Components;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Backmen.Shipwrecked.Components;
using Content.Server.Parallax;
using Content.Server.Shuttles.Systems;
using Content.Shared._Lavaland.Procedural.Prototypes;
using Content.Shared.Atmos;
using Content.Shared.CCVar;
using Content.Shared.GameTicking;
using Content.Shared.Gravity;
using Content.Shared.Movement.Components;
using Content.Shared.Parallax.Biomes;
using Content.Shared.Physics;
using Content.Shared.Salvage;
using Content.Shared.Shuttles.Components;
using Robust.Shared.Configuration;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Collision.Shapes;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Utility;

namespace Content.Server._Lavaland.Procedural.Systems;

/// <summary>
/// Basic system to create Lavaland planet.
/// </summary>
public sealed class LavalandPlanetSystem : EntitySystem
{
    /// <summary>
    /// Whether lavaland is enabled or not.
    /// </summary>
    public bool LavalandEnabled = true;

    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly INetConfigurationManager _config = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly AtmosphereSystem _atmos = default!;
    [Dependency] private readonly BiomeSystem _biome = default!;
    [Dependency] private readonly FixtureSystem _fixtures = default!;
    [Dependency] private readonly MetaDataSystem _metaData = default!;
    [Dependency] private readonly MapLoaderSystem _mapLoader = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly ShuttleSystem _shuttle = default!;

    private EntityQuery<TransformComponent> _xformQuery;
    private EntityQuery<FixturesComponent> _fixtureQuery;

    protected override string SawmillName { get; } = "lavaland";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);

        _xformQuery = GetEntityQuery<TransformComponent>();
        _fixtureQuery = GetEntityQuery<FixturesComponent>();

        Subs.CVar(_config, CCVars.LavalandEnabled, value => LavalandEnabled = value, true);
    }

    #region Events

    private void OnRoundRestart(RoundRestartCleanupEvent ev)
    {
        var ent = GetPreloaderEntity();
        if (ent == null)
            return;

        Del(ent.Value.Owner);
    }

    #endregion

    #region Public API

    public void EnsurePreloaderMap()
    {
        // Already have a preloader?
        if (GetPreloaderEntity() != null)
            return;

        if (!LavalandEnabled)
            return;

        var mapUid = _map.CreateMap(out var mapId, false);
        EnsureComp<LavalandPreloaderComponent>(mapUid);
        _metaData.SetEntityName(mapUid, "Lavaland Preloader Map");
        _map.SetPaused(mapId, true);
    }

    public Entity<LavalandPreloaderComponent>? GetPreloaderEntity()
    {
        var query = AllEntityQuery<LavalandPreloaderComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            return (uid, comp);
        }

        return null;
    }

    public IEnumerable<Entity<LavalandMapComponent>> GetLavalands()
    {
        var lavalandsQuery = EntityQueryEnumerator<LavalandMapComponent>();
        while (lavalandsQuery.MoveNext(out var uid, out var comp))
        {
            yield return (uid, comp);
        }
    }

    /// <summary>
    /// Setup ALL instances of LavalandMapPrototype.
    /// </summary>
    public void SetupLavalands()
    {
        foreach (var lavaland in _proto.EnumeratePrototypes<LavalandMapPrototype>())
        {
            if (!SetupLavalandPlanet(out _, lavaland))
            {
                Log.Error($"Failed to load lavaland planet: {lavaland.ID}");
            }
        }
    }

    #endregion

    #region Planet Systems

    public bool SetupLavalandPlanet(
        out Entity<LavalandMapComponent>? lavaland,
        LavalandMapPrototype prototype,
        int? seed = null,
        Entity<LavalandPreloaderComponent>? preloader = null)
    {
        lavaland = null;

        if (preloader == null)
        {
            preloader = GetPreloaderEntity();
            if (preloader == null)
                return false;
        }

        // Basic setup.
        var lavalandMap = _map.CreateMap(out var lavalandMapId, runMapInit: false);
        var mapComp = EnsureComp<LavalandMapComponent>(lavalandMap);
        lavaland = (lavalandMap, mapComp);

        // If specified, force new seed
        seed ??= _random.Next();

        var lavalandPrototypeId = prototype.ID;

        PlanetBasicSetup(lavalandMap, prototype, seed.Value);

        // Ensure that it's paused
        _map.SetPaused(lavalandMapId, true);

        if (!SetupOutpost(lavalandMap, lavalandMapId, prototype.OutpostPath, out var outpost))
            return false;

        var loadBox = Box2.CentredAroundZero(new Vector2(prototype.RestrictedRange * 2, prototype.RestrictedRange * 2));

        mapComp.Outpost = outpost;
        mapComp.Seed = seed.Value;
        mapComp.PrototypeId = lavalandPrototypeId;

        EnsureComp<BiomeOptimizeComponent>(lavalandMap).LoadArea = loadBox;

        // Setup Ruins.
        var pool = _proto.Index(prototype.RuinPool);
        SetupRuins(pool, lavaland.Value, preloader.Value);

        // Hide all grids from the mass scanner.
        foreach (var grid in _mapManager.GetAllGrids(lavalandMapId))
        {
            var flag = IFFFlags.Hide;

            #if DEBUG || TOOLS
            flag = IFFFlags.HideLabel;
            #endif

            _shuttle.AddIFFFlag(grid, flag);
        }

        // Start!!1!!!
        _map.InitializeMap(lavalandMapId);

        // also preload the planet itself
        _biome.Preload(lavalandMap, Comp<BiomeComponent>(lavalandMap), loadBox);

        // Finally add destination
        var dest = AddComp<FTLDestinationComponent>(lavalandMap);
        dest.Whitelist = prototype.ShuttleWhitelist;

        return true;
    }

    private void PlanetBasicSetup(EntityUid lavalandMap, LavalandMapPrototype prototype, int seed)
    {
        // Name
        _metaData.SetEntityName(lavalandMap, Loc.GetString(prototype.Name));

        // Biomes
        _biome.EnsurePlanet(lavalandMap, _proto.Index(prototype.BiomePrototype), seed, mapLight: prototype.PlanetColor);

        // Marker Layers
        var biome = EnsureComp<BiomeComponent>(lavalandMap);
        foreach (var marker in prototype.OreLayers)
        {
            _biome.AddMarkerLayer(lavalandMap, biome, marker);
        }
        Dirty(lavalandMap, biome);

        // Gravity
        var gravity = EnsureComp<GravityComponent>(lavalandMap);
        gravity.Enabled = true;
        Dirty(lavalandMap, gravity);

        // Atmos
        var air = prototype.Atmosphere;
        // copy into a new array since the yml deserialization discards the fixed length
        var moles = new float[Atmospherics.AdjustedNumberOfGases];
        air.CopyTo(moles, 0);

        var atmos = EnsureComp<MapAtmosphereComponent>(lavalandMap);
        _atmos.SetMapGasMixture(lavalandMap, new GasMixture(moles, prototype.Temperature), atmos);

        // Restricted Range
        var restricted = new RestrictedRangeComponent
        {
            Range = prototype.RestrictedRange,
        };
        AddComp(lavalandMap, restricted);

        // TODO: Make Restricted Range a square, because lavaland is... Well, a square.
    }

    private bool SetupOutpost(EntityUid lavaland, MapId lavalandMapId, ResPath path, out EntityUid outpost)
    {
        outpost = EntityUid.Invalid;

        // Setup Outpost
        if (!_mapLoader.TryLoadGrid(lavalandMapId, path, out var outpostGrid))
        {
            Log.Error("Failed to load Lavaland outpost!");
            return false;
        }

        outpost = outpostGrid.Value;

        // Align outpost to planet
        _transform.SetCoordinates(outpost, new EntityCoordinates(lavaland, 0, 0));

        // Name it
        _metaData.SetEntityName(outpost, Loc.GetString("lavaland-planet-outpost"));
        var member = EnsureComp<LavalandMemberComponent>(outpost);
        member.SignalName = Loc.GetString("lavaland-planet-outpost");

        return true;
    }

    #endregion

    #region Ruin Generation

    private void SetupRuins(LavalandRuinPoolPrototype pool, Entity<LavalandMapComponent> lavaland, Entity<LavalandPreloaderComponent> preloader)
    {
        var random = new Random(lavaland.Comp.Seed);

        var boundary = GetOutpostBoundary(lavaland);
        if (boundary == null)
            return;

        var coords = GetCoordinates(pool.RuinDistance, pool.MaxDistance);
        random.Shuffle(coords);
        var usedSpace = new List<Box2> { boundary.Value };

        // Load grid ruins
        SetupHugeRuins(pool.GridRuins, lavaland, preloader, random, pool.RuinDistance, ref coords, ref usedSpace);

        // Create a new list that excludes all already used spaces that intersect with big ruins.
        // Sweet optimization (another lag machine).
        var newCoords = coords.ToHashSet();
        foreach (var usedBox in usedSpace)
        {
            var list = coords.Where(coord => !usedBox.Contains(coord)).ToHashSet();
            newCoords = newCoords.Concat(list).ToHashSet();
        }

        coords = newCoords.ToList();

        // Load dungeon ruins
        // TODO: make it actual dungeons instead of spawning markers
        SetupDungeonRuins(pool.DungeonRuins, lavaland, random, pool.RuinDistance, ref coords, ref usedSpace);
    }

    /// <summary>
    /// Contains all already calculated ruin bounds to fastly reuse them in new rounds.
    /// </summary>
    private Dictionary<string, Box2> _ruinBoundariesDict = new();

    private void SetupHugeRuins(
        Dictionary<ProtoId<LavalandGridRuinPrototype>, ushort> ruins,
        Entity<LavalandMapComponent> lavaland,
        Entity<LavalandPreloaderComponent> preloader,
        Random random,
        float ruinDistance,
        ref List<Vector2> coords,
        ref List<Box2> usedSpace)
    {
        // Get and sort all ruins, because we can't sort dictionaries
        var list = GetGridRuinProtos(ruins);
        list.Sort((x, y) => x.Priority.CompareTo(y.Priority));

        // Place them down randomly
        foreach (var ruin in list)
        {
            var attempts = 0;
            while (!LoadGridRuin(ruin, lavaland, preloader, random, ref _ruinBoundariesDict, ref usedSpace, ref coords, out var spawned))
            {
                attempts++;
                if (attempts > ruin.SpawnAttemps)
                    break;
            }
        }
    }

    private void SetupDungeonRuins(
        Dictionary<ProtoId<LavalandDungeonRuinPrototype>, ushort> ruins,
        Entity<LavalandMapComponent> lavaland,
        Random random,
        float ruinDistance,
        ref List<Vector2> coords,
        ref List<Box2> usedSpace)
    {
        // Get and sort all ruins, because we can't sort dictionaries
        var list = GetDungeonRuinProtos(ruins);
        list.Sort((x, y) => x.Priority.CompareTo(y.Priority));

        // Place them down randomly
        foreach (var ruin in list)
        {
            var attempts = 0;
            while (!LoadDungeonRuin(ruin, lavaland, random, ref usedSpace, ref coords))
            {
                attempts++;
                if (attempts > ruin.SpawnAttemps)
                    break;
            }
        }
    }

    private Box2? GetOutpostBoundary(Entity<LavalandMapComponent> lavaland, FixturesComponent? manager = null, TransformComponent? xform = null)
    {
        var uid = lavaland.Comp.Outpost;

        if (!Resolve(uid, ref manager, ref xform) || xform.MapUid != lavaland)
            return null;

        var aabbs = new Box2();

        var transform = _physics.GetRelativePhysicsTransform((uid, xform), xform.MapUid.Value);
        foreach (var fixture in manager.Fixtures.Values)
        {
            if (!fixture.Hard)
                return null;

            var aabb = fixture.Shape.ComputeAABB(transform, 0);
            aabbs = aabbs.Union(aabb);
        }

        aabbs = aabbs.Enlarged(8f);
        return aabbs;
    }

    private bool LoadGridRuin(
        LavalandGridRuinPrototype ruin,
        Entity<LavalandMapComponent> lavaland,
        Entity<LavalandPreloaderComponent> preloader,
        Random random,
        ref Dictionary<string, Box2> ruinsBoundsDict,
        ref List<Box2> usedSpace,
        ref List<Vector2> coords,
        [NotNullWhen(true)] out EntityUid? spawned)
    {
        spawned = null;
        if (coords.Count == 0)
            return false;

        var coord = random.Pick(coords);
        var mapXform = Transform(preloader);
        Box2 ruinBox; // This is ruin box, but moved to it's correct coords on the map

        // Check if we already calculated that boundary before, and if we didn't then calculate it now
        if (!ruinsBoundsDict.TryGetValue(ruin.ID, out var box))
        {
            if (!_mapLoader.TryLoadGrid(mapXform.MapID, ruin.Path, out var spawnedBoundedGrid))
            {
                Log.Error($"Failed to load ruin {ruin.ID} onto dummy map, on stage of loading! AAAAA!!");
                return false;
            }

            // It's not useless!
            spawned = spawnedBoundedGrid.Value.Owner;

            if (!_fixtureQuery.TryGetComponent(spawned, out var manager))
            {
                Log.Error($"Failed to load ruin {ruin.ID} onto dummy map, it doesn't have fixture component! AAAAA!!");
                Del(spawned);
                return false;
            }

            // Actually calculate ruin bound
            var transform = _physics.GetRelativePhysicsTransform(spawned.Value, preloader.Owner);
            // holy shit
            var bounds = (from fixture in manager.Fixtures.Values where fixture.Hard select fixture.Shape.ComputeAABB(transform, 0).Rounded(0)).ToList();
            // Round this list of boxes up to
            var calculatedBox = _random.Pick(bounds);
            foreach (var bound in bounds)
            {
                calculatedBox = calculatedBox.Union(bound);
            }

            // Safety measure
            calculatedBox = calculatedBox.Enlarged(8f);

            // Add calculated box to dictionary
            ruinsBoundsDict.Add(ruin.ID, calculatedBox);

            // Move our calculated box to correct position
            var v1 = calculatedBox.BottomLeft + coord;
            var v2 = calculatedBox.TopRight + coord;
            ruinBox = new Box2(v1, v2);

            // Teleport it into place on preloader map
            _transform.SetCoordinates(spawned.Value, new EntityCoordinates(preloader, coord));
        }
        else
        {
            // Why there's no method to move the Box2 around???
            var v1 = box.BottomLeft + coord;
            var v2 = box.TopRight + coord;
            ruinBox = new Box2(v1, v2);
        }

        // If any used boundary intersects with current boundary, return
        if (usedSpace.Any(used => used.Intersects(ruinBox)))
        {
            Log.Debug("Ruin can't be placed on it's coordinates, skipping spawn");
            return false;
        }

        // Try to load it on a dummy map if it wasn't already
        if (spawned == null)
        {
            if (!_mapLoader.TryLoadGrid(mapXform.MapID, ruin.Path, out var spawnedGrid, offset: coord))
            {
                Log.Error($"Failed to load ruin {ruin.ID} onto dummy map, on stage of reparenting it to Lavaland! (this is really bad)");
                return false;
            }

            spawned = spawnedGrid.Value.Owner;
        }

        // Set its position to Lavaland
        var spawnedXForm = _xformQuery.GetComponent(spawned.Value);
        _metaData.SetEntityName(spawned.Value, Loc.GetString(ruin.Name));
        _transform.SetParent(spawned.Value, spawnedXForm, lavaland);
        _transform.SetCoordinates(spawned.Value, new EntityCoordinates(lavaland, spawnedXForm.Coordinates.Position.Rounded()));

        // yaaaaaaaaaaaaaaaay
        usedSpace.Add(ruinBox);
        coords.Remove(coord);
        return true;
    }

    private bool LoadDungeonRuin(
        LavalandDungeonRuinPrototype ruin,
        Entity<LavalandMapComponent> lavaland,
        Random random,
        ref List<Box2> usedSpace,
        ref List<Vector2> coords)
    {
        if (coords.Count == 0)
            return false;

        var coord = random.Pick(coords);
        var box = Box2.CentredAroundZero(ruin.Boundary);

        // Why there's no method to move the Box2 around???
        var v1 = box.BottomLeft + coord;
        var v2 = box.TopRight + coord;
        var ruinBox = new Box2(v1, v2); // This is ruin box, but moved to it's correct coords on the map

        // If any used boundary intersects with current boundary, return
        if (usedSpace.Any(used => used.Intersects(ruinBox)))
        {
            Log.Debug("Ruin can't be placed on it's coordinates, skipping spawn");
            return false;
        }

        // Spawn the marker
        Spawn(ruin.SpawnedMarker, new EntityCoordinates(lavaland, coord));

        usedSpace.Add(ruinBox);
        coords.Remove(coord);
        return true;
    }

    #endregion

    #region Helper Methods

    private List<Vector2> GetCoordinates(float distance, float maxDistance)
    {
        var coords = new List<Vector2>();
        var moveVector = new Vector2(maxDistance, maxDistance);

        while (moveVector.Y >= -maxDistance)
        {
            // i love writing shitcode
            // Moving like a snake through the entire map placing all dots onto its places.

            while (moveVector.X > -maxDistance)
            {
                coords.Add(moveVector);
                moveVector += new Vector2(-distance, 0);
            }

            coords.Add(moveVector);
            moveVector += new Vector2(0, -distance);

            while (moveVector.X < maxDistance)
            {
                coords.Add(moveVector);
                moveVector += new Vector2(distance, 0);
            }

            coords.Add(moveVector);
            moveVector += new Vector2(0, -distance);
        }

        return coords;
    }

    private List<LavalandGridRuinPrototype> GetGridRuinProtos(Dictionary<ProtoId<LavalandGridRuinPrototype>, ushort> protos)
    {
        var list = new List<LavalandGridRuinPrototype>();

        foreach (var (protoId, count) in protos)
        {
            var proto = _proto.Index(protoId);
            for (var i = 0; i < count; i++)
            {
                list.Add(proto);
            }
        }

        return list;
    }

    private List<LavalandDungeonRuinPrototype> GetDungeonRuinProtos(Dictionary<ProtoId<LavalandDungeonRuinPrototype>, ushort> protos)
    {
        var list = new List<LavalandDungeonRuinPrototype>();
        foreach (var (protoId, count) in protos)
        {
            var proto = _proto.Index(protoId);
            for (var i = 0; i < count; i++)
            {
                list.Add(proto);
            }
        }

        return list;
    }

    #endregion
}
