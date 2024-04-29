using System.Linq;
using Content.Server.GameTicking;
using Content.Shared.GameTicking;
using Robust.Server.GameObjects;
using Robust.Server.Maps;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Content.Server.Spawners.Components;
using Robust.Shared.Random;
using Robust.Server.Player;
using Content.Server.Chat.Systems;
using Content.Server.Station.Systems;
using Robust.Shared.Utility;
using Robust.Shared.Audio;
using System.Threading;
using Content.Server.Actions;
using Content.Server.Ghost.Roles.Components;
using Content.Server.RandomMetadata;
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

    [ViewVariables] public List<SpecForcesHistory> CalledEvents { get; private set; } = new();
    [ViewVariables] public TimeSpan LastUsedTime { get; private set; } = TimeSpan.Zero;
    private readonly TimeSpan _delayUsage = TimeSpan.FromMinutes(2);
    private readonly ReaderWriterLockSlim _callLock = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SpecForceComponent, MapInitEvent>(OnMapInit, after: new[] { typeof(RandomMetadataSystem) });
        SubscribeLocalEvent<RoundEndTextAppendEvent>(OnRoundEnd);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnCleanup);
        SubscribeLocalEvent<SpecForceComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<SpecForceComponent, ComponentShutdown>(OnShutdown);
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
            comp.Owner = uid;
            EntityManager.AddComponent(uid, comp);
        }
    }

    public TimeSpan DelayTime
    {
        get
        {
            var ct = _gameTicker.RoundDuration();
            var lastUsedTime = LastUsedTime + _delayUsage;
            return ct > lastUsedTime ? TimeSpan.Zero : lastUsedTime - ct;
        }
    }

    public bool CallOps(string protoId, string source = "")
    {
        _callLock.EnterWriteLock();
        try
        {
            if (_gameTicker.RunLevel != GameRunLevel.InRound)
            {
                return false;
            }

            var currentTime = _gameTicker.RoundDuration();

#if !DEBUG
            if (LastUsedTime + _delayUsage > currentTime)
            {
                return false;
            }
#endif

            LastUsedTime = currentTime;

            if (!_prototypes.TryIndex<SpecForceTeamPrototype>(protoId, out var prototype))
            {
                throw new ArgumentException("Wrong SpecForceTeamPrototype ID!");
            }

            CalledEvents.Add(new SpecForcesHistory { Event = prototype.SpecForceName, RoundTime = currentTime, WhoCalled = source });

            var shuttle = SpawnShuttle(prototype.ShuttlePath);
            if (shuttle == null)
            {
                return false;
            }

            SpawnGhostRole(prototype, shuttle.Value);

            PlaySound(prototype);

            return true;
        }
        finally
        {
            _callLock.ExitWriteLock();
        }
    }

    private EntityUid SpawnEntity(string? protoName, EntityCoordinates coordinates)
    {
        if (protoName == null)
        {
            return EntityUid.Invalid;
        }

        var uid = EntityManager.SpawnEntity(protoName, coordinates);

        if (!TryComp<GhostRoleMobSpawnerComponent>(uid, out var mobSpawnerComponent) ||
            mobSpawnerComponent.Prototype == null ||
            !_prototypes.TryIndex<EntityPrototype>(mobSpawnerComponent.Prototype, out var spawnObj))
        {
            return uid;
        }

        if (spawnObj.TryGetComponent<SpecForceComponent>(out var tplSpecForceComponent))
        {
            var comp = (Component) _serialization.CreateCopy(tplSpecForceComponent, notNullableOverride: true);
            comp.Owner = uid;
            EntityManager.AddComponent(uid, comp);
        }

        EnsureComp<SpecForceComponent>(uid);
        if (spawnObj.TryGetComponent<GhostRoleComponent>(out var tplGhostRoleComponent))
        {
            var comp = (Component) _serialization.CreateCopy(tplGhostRoleComponent, notNullableOverride: true);
            comp.Owner = uid;
            EntityManager.AddComponent(uid, comp);
        }

        return uid;
    }

    private void SpawnGhostRole(SpecForceTeamPrototype proto, EntityUid shuttle)
    {
        var spawns = new List<EntityCoordinates>();

        foreach (var (_, meta, xform) in EntityManager
                     .EntityQuery<SpawnPointComponent, MetaDataComponent, TransformComponent>(true))
        {
            if (meta.EntityPrototype?.ID != proto.SpawnMarker)
                continue;

            if (xform.ParentUid != shuttle)
                continue;

            spawns.Add(xform.Coordinates);
            break;
        }

        if (spawns.Count == 0)
        {
            spawns.Add(Transform(shuttle).Coordinates);
        }

        // Spawn Guaranteed SpecForces from the prototype.
        foreach (var mob in proto.GuaranteedSpawn)
        {
            SpawnEntity(mob, _random.Pick(spawns));
        }

        // Count how many other forces there should be.
        var countExtra = _playerManager.PlayerCount / proto.SpawnPerPlayers;
        countExtra = Math.Max(0, countExtra - proto.GuaranteedSpawn.Count); // Either zero or bigger than zero, no negatives
        countExtra = Math.Min(countExtra, proto.MaxRolesAmount - proto.GuaranteedSpawn.Count); // If bigger than MaxAmount, set to MaxAmount and extract already spawned roles

        // Spawn Guaranteed SpecForces from the prototype.
        // If all mobs from the list are spawned and we still have free slots, restart the cycle again.
        while (countExtra > 0)
        {
            foreach (var mob in proto.SpecForceSpawn.Where(mob => countExtra > 0))
            {
                countExtra--;
                SpawnEntity(mob, _random.Pick(spawns));
            }
        }
    }

    /// <summary>
    /// Spawns shuttle for SpecForces on a new map.
    /// </summary>
    /// <param name="shuttlePath"></param>
    /// <returns>Grid of the shuttle</returns>
    private EntityUid? SpawnShuttle(string shuttlePath)
    {
        var shuttleMap = _mapManager.CreateMap();
        var options = new MapLoadOptions()
        {
            LoadMap = true
        };

        if (!_map.TryLoad(shuttleMap, shuttlePath, out var grids, options))
        {
            return null;
        }

        var mapGrid = grids.FirstOrNull();

        return mapGrid ?? null;
    }

    private void PlaySound(SpecForceTeamPrototype proto)
    {
        var stations = _stationSystem.GetStations();
        if (stations.Count == 0)
        {
            return;
        }

        // TODO: This probably needs refactor
        if (proto.AnnouncementText == null || proto.AnnouncementTitle == null)
            return;

        if (proto.AnnouncementSoundPath != null)
        {
            var announcementSound = new SoundPathSpecifier(proto.AnnouncementSoundPath);
            foreach (var station in stations)
            {
                _chatSystem.DispatchStationAnnouncement(station,
                    Loc.GetString(proto.AnnouncementText),
                    Loc.GetString(proto.AnnouncementTitle),
                    false, announcementSound);
            }
        }
        else
        {
            foreach (var station in stations)
            {
                _chatSystem.DispatchStationAnnouncement(station,
                    Loc.GetString(proto.AnnouncementText),
                    Loc.GetString(proto.AnnouncementTitle),
                    false);
            }
        }
    }

    private void OnRoundEnd(RoundEndTextAppendEvent ev)
    {
        foreach (var calledEvent in CalledEvents)
        {
            ev.AddLine(Loc.GetString("spec-forces-system-round-end",
                ("specforce", calledEvent.Event),
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
    }
}
