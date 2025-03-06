using System.Linq;
using System.Numerics;
using Content.Server.GameTicking;
using Content.Shared.GameTicking;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Content.Server.Spawners.Components;
using Robust.Shared.Random;
using Robust.Server.Player;
using Content.Server.Chat.Systems;
using Content.Server.Station.Systems;
using Content.Shared.Storage;
using Robust.Shared.Utility;
using System.Threading;
using Content.Server.Actions;
using Content.Server.Backmen.Blob;
using Content.Server.Backmen.Blob.Components;
using Content.Server.Backmen.GameTicking.Rules.Components;
using Content.Server.Ghost.Roles.Components;
using Content.Server.RandomMetadata;
using Content.Shared.Backmen.CCVar;
using Content.Shared.Ghost.Roles.Components;
using Robust.Shared.Configuration;
using Robust.Shared.EntitySerialization;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Map.Components;
using Robust.Shared.Serialization.Manager;

namespace Content.Server.Backmen.SpecForces;

public sealed class SpecForcesSystem : EntitySystem
{
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly MapLoaderSystem _map = default!;
    [Dependency] private readonly GameTicker _gameTicker = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly ChatSystem _chatSystem = default!;
    [Dependency] private readonly StationSystem _stationSystem = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly ISerializationManager _serialization = default!;
    [Dependency] private readonly ActionsSystem _actions = default!;
    [Dependency] private readonly IConfigurationManager _configurationManager = default!;
    [Dependency] private readonly IComponentFactory _componentFactory = default!;

    [ViewVariables] public List<SpecForcesHistory> CalledEvents { get; } = new();
    [ViewVariables] public TimeSpan LastUsedTime { get; private set; } = TimeSpan.Zero;
    private readonly ReaderWriterLockSlim _callLock = new();
    private TimeSpan DelayUsage => TimeSpan.FromMinutes(_configurationManager.GetCVar(CCVars.SpecForceDelay));

    public MapId? ShipyardMap { get; private set; }
    private float _shuttleIndex;
    private const float ShuttleSpawnBuffer = 1f;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SpecForceComponent, MapInitEvent>(OnMapInit, after: [typeof(RandomMetadataSystem)]);
        SubscribeLocalEvent<RoundEndTextAppendEvent>(OnRoundEnd);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnCleanup);
        SubscribeLocalEvent<SpecForceComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<SpecForceComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<BlobChangeLevelEvent>(OnBlobChange);
    }

    private void SetupShipyard()
    {
        if (ShipyardMap != null && _mapManager.MapExists(ShipyardMap.Value))
            return;

        ShipyardMap = _mapManager.CreateMap();

        _mapManager.SetMapPaused(ShipyardMap.Value, false);
        _shuttleIndex = 0;
    }
    private void CleanupShipyard()
    {
        if (ShipyardMap == null || !_mapManager.MapExists(ShipyardMap.Value))
        {
            ShipyardMap = null;
            _shuttleIndex = 0;
            return;
        }

        _mapManager.DeleteMap(ShipyardMap.Value);
        ShipyardMap = null;
        _shuttleIndex = 0;
    }

    [ValidatePrototypeId<SpecForceTeamPrototype>]
    private const string Rxbzz = "RXBZZBlobDefault";

    private void OnBlobChange(BlobChangeLevelEvent ev)
    {
        if (ev.Level != BlobStage.Critical)
            return;

        var blobConfig = CompOrNull<StationBlobConfigComponent>(ev.Station);
        var specForceTeam = blobConfig?.SpecForceTeam ?? Rxbzz;
        if (blobConfig?.SpecForceTeam == null)
        {
            Log.Info("Station doesn't have it's preferable SpecForceTeam in BlobConfig. Calling default squad...");
        }

        if (!_prototypes.TryIndex(specForceTeam, out var prototype) ||
            !CallOps(prototype.ID, "ДСО", null, true))
        {
            Log.Error($"Failed to spawn {specForceTeam} SpecForce for the blob GameRule!");
        }
    }

    private void OnShutdown(EntityUid uid, SpecForceComponent component, ComponentShutdown args)
    {
        _actions.RemoveAction(uid, component.BssKey);
    }

    private void OnStartup(EntityUid uid, SpecForceComponent component, ComponentStartup args)
    {
        if (component.ActionBssActionName != null)
            _actions.AddAction(uid, ref component.BssKey, component.ActionBssActionName);
    }

    private void OnMapInit(EntityUid uid, SpecForceComponent component, MapInitEvent args)
    {
        foreach (var entry in component.Components.Values)
        {
            var comp = (Component) _serialization.CreateCopy(entry.Component, notNullableOverride: true);
            EntityManager.AddComponent(uid, comp);
        }
    }

    public TimeSpan DelayTime
    {
        get
        {
            var ct = _gameTicker.RoundDuration();
            var lastUsedTime = LastUsedTime + DelayUsage;
            return ct > lastUsedTime ? TimeSpan.Zero : lastUsedTime - ct;
        }
    }

    /// <summary>
    /// Calls SpecForce team, creating new map with a shuttle, and spawning on it SpecForces.
    /// </summary>
    /// <param name="protoId"> SpecForceTeamPrototype ID.</param>
    /// <param name="source"> Source of the call.</param>
    /// <param name="forceCountExtra"> How many extra SpecForces will be forced to spawn.</param>
    /// <param name="forceCall"> If true, cooldown will be ignored.</param>
    /// <returns>Returns true if call was successful.</returns>
    public bool CallOps(ProtoId<SpecForceTeamPrototype> protoId, string source = "Unknown", int? forceCountExtra = null, bool forceCall = false)
    {
        if (!_callLock.TryEnterWriteLock(-1))
        {
            Log.Warning("SpecForces is busy!");
            return false;
        }
        try
        {
            if (!_prototypes.TryIndex(protoId, out var prototype))
            {
                Log.Error("Wrong SpecForceTeamPrototype ID!");
                return false;
            }

            if (_gameTicker.RunLevel != GameRunLevel.InRound)
            {
                Log.Warning("Can't call SpecForces while not in the round.");
                return false;
            }

            var currentTime = _gameTicker.RoundDuration();

#if !DEBUG
            if (LastUsedTime + DelayUsage > currentTime && !forceCall)
            {
                Log.Info("Tried to call SpecForce when it's on cooldown.");
                return false;
            }
#endif

            LastUsedTime = currentTime;

            var shuttle = SpawnShuttle(prototype.ShuttlePath);
            if (shuttle == null)
            {
                Log.Error("Failed to load SpecForce shuttle!");
                return false;
            }

            SpawnGhostRole(prototype, shuttle.Value, forceCountExtra);
            DispatchAnnouncement(prototype);

            Log.Info($"Successfully called {prototype.ID} SpecForceTeam. Source: {source}");

            CalledEvents.Add(new SpecForcesHistory { Event = prototype.SpecForceName, RoundTime = currentTime, WhoCalled = source });

            return true;
        }
        finally
        {
            _callLock.ExitWriteLock();
        }
    }

    private EntityUid SpawnEntity(string? protoName, EntityCoordinates coordinates, SpecForceTeamPrototype specforce)
    {
        if (protoName == null)
            return EntityUid.Invalid;

        var uid = EntityManager.SpawnEntity(protoName, coordinates);

        EnsureComp<SpecForceComponent>(uid);

        // If entity is a GhostRoleMobSpawner, it's child prototype is valid AND
        // has GhostRoleComponent, clone this component and add it to the parent.
        // This is necessary for SpawnMarkers that don't have GhostRoleComp in prototype.
        if (TryComp<GhostRoleMobSpawnerComponent>(uid, out var mobSpawnerComponent) &&
            mobSpawnerComponent.Prototype != null &&
            _prototypes.TryIndex<EntityPrototype>(mobSpawnerComponent.Prototype, out var spawnObj) &&
            spawnObj.TryGetComponent<GhostRoleComponent>(out var tplGhostRoleComponent, _componentFactory))
        {
            var comp = _serialization.CreateCopy(tplGhostRoleComponent, notNullableOverride: true);
            comp.RaffleConfig = specforce.RaffleConfig;
            EntityManager.AddComponent(uid, comp);
        }

        if (TryComp<GhostRoleComponent>(uid, out var ghostRole) && ghostRole.RaffleConfig == null)
        {
            ghostRole.RaffleConfig = specforce.RaffleConfig;
        }

        return uid;
    }

    public int GetOptIdCount(SpecForceTeamPrototype proto, int? plrCount = null) =>
        ((plrCount ?? _playerManager.PlayerCount) + proto.SpawnPerPlayers) / proto.SpawnPerPlayers;

    private void SpawnGhostRole(SpecForceTeamPrototype proto, EntityUid shuttle, int? forceCountExtra = null)
    {
        // Find all spawn points on the shuttle, add them in list
        var spawns = new List<EntityCoordinates>();
        var query = EntityQueryEnumerator<SpawnPointComponent, MetaDataComponent, TransformComponent>();
        while (query.MoveNext(out _, out var meta, out var xform))
        {
            if (meta.EntityPrototype!.ID != proto.SpawnMarker)
                continue;

            if (xform.GridUid != shuttle)
                continue;

            spawns.Add(xform.Coordinates);
        }

        if (spawns.Count == 0)
        {
            Log.Warning("Shuttle has no valid spawns for SpecForces! Making something up...");
            spawns.Add(Transform(shuttle).Coordinates);
        }

        SpawnGuaranteed(proto, spawns);
        SpawnSpecForces(proto, spawns, forceCountExtra);
    }

    private void SpawnGuaranteed(SpecForceTeamPrototype proto, List<EntityCoordinates> spawns)
    {
        // If specForceSpawn is empty, we can't continue
        if (proto.GuaranteedSpawn.Count == 0)
            return;

        // Spawn Guaranteed SpecForces from the prototype.
        var toSpawnGuaranteed = EntitySpawnCollection.GetSpawns(proto.GuaranteedSpawn, _random);

        foreach (var mob in toSpawnGuaranteed)
        {
            var spawned = SpawnEntity(mob, _random.Pick(spawns), proto);
            Log.Info($"Successfully spawned {ToPrettyString(spawned)} Static SpecForce.");
        }
    }

    private void SpawnSpecForces(SpecForceTeamPrototype proto, List<EntityCoordinates> spawns, int? forceCountExtra)
    {
        // If specForceSpawn is empty, we can't continue
        if (proto.SpecForceSpawn.Count == 0)
            return;

        // Count how many other forces there should be.
        var countExtra = GetOptIdCount(proto);
        // If bigger than MaxAmount, set to MaxAmount and extract already spawned roles
        countExtra = Math.Min(countExtra, proto.MaxRolesAmount);

        // If CountExtra was forced to some number, check if this number is in range and extract already spawned roles.
        if (forceCountExtra is >= 0 and <= 15)
            countExtra = forceCountExtra.Value;

        // Either zero or bigger than zero, no negatives
        countExtra = Math.Max(0, countExtra);

        // Spawn Guaranteed SpecForces from the prototype.
        // If all mobs from the list are spawned and we still have free slots, restart the cycle again.
        while (countExtra > 0)
        {
            var toSpawnForces = EntitySpawnCollection.GetSpawns(proto.SpecForceSpawn, _random);
            foreach (var mob in toSpawnForces.Where( _ => countExtra > 0))
            {
                countExtra--;
                var spawned = SpawnEntity(mob, _random.Pick(spawns), proto);
                Log.Info($"Successfully spawned {ToPrettyString(spawned)} Opt-in SpecForce.");
            }
        }
    }

    /// <summary>
    /// Spawns shuttle for SpecForces on a new map.
    /// </summary>
    /// <param name="shuttlePath"></param>
    /// <returns>Grid's entity of the shuttle.</returns>
    private EntityUid? SpawnShuttle(string shuttlePath)
    {
        SetupShipyard();



        if (!_map.TryLoadGrid(ShipyardMap!.Value, new ResPath(shuttlePath), out var grid, offset: new Vector2(500f + _shuttleIndex, 1f)))
        {
            return null;
        }

        _shuttleIndex += grid.Value.Comp.LocalAABB.Width + 1;

        return grid;
    }

    private void DispatchAnnouncement(SpecForceTeamPrototype proto)
    {
        var stations = _stationSystem.GetStations();
        var playTts = false;

        if (stations.Count == 0)
            return;

        // If we don't have title or text for the announcement, we can't make the announcement.
        if (proto.AnnouncementText == null || proto.AnnouncementTitle == null)
            return;

        // No SoundSpecifier provided - play standard announcement sound
        if (proto.AnnouncementSoundPath == default!)
            playTts = true;

        foreach (var station in stations)
        {
            _chatSystem.DispatchStationAnnouncement(station,
                Loc.GetString(proto.AnnouncementText),
                Loc.GetString(proto.AnnouncementTitle),
                playTts,
                proto.AnnouncementSoundPath);
        }
    }

    private void OnRoundEnd(RoundEndTextAppendEvent ev)
    {
        foreach (var calledEvent in CalledEvents)
        {
            ev.AddLine(Loc.GetString("spec-forces-system-round-end",
                ("specforce", Loc.GetString(calledEvent.Event)),
                ("time", calledEvent.RoundTime.ToString(@"hh\:mm\:ss")),
                ("who", calledEvent.WhoCalled)));
        }
    }

    private void OnCleanup(RoundRestartCleanupEvent ev)
    {
        CalledEvents.Clear();
        LastUsedTime = TimeSpan.Zero;

        if (_callLock.IsWriteLockHeld)
        {
            _callLock.ExitWriteLock();
        }

        CleanupShipyard();
    }
}
