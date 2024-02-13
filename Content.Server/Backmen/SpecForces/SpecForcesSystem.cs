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
    // ReSharper disable once MemberCanBePrivate.Global
    [ViewVariables] public List<SpecForcesHistory> CalledEvents { get; private set; } = new List<SpecForcesHistory>();
    // ReSharper disable once MemberCanBePrivate.Global
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
        if (component.Components != null)
        {
            foreach (var entry in component.Components.Values)
            {
                var comp = (Component) _serialization.CreateCopy(entry.Component, notNullableOverride: true);
                comp.Owner = uid;
                EntityManager.AddComponent(uid, comp);
            }
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

    public bool CallOps(SpecForcesType ev, string source = "")
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

            CalledEvents.Add(new SpecForcesHistory { Event = ev, RoundTime = currentTime, WhoCalled = source });

            var shuttle = SpawnShuttle(ev);
            if (shuttle == null)
            {
                return false;
            }

            SpawnGhostRole(ev, shuttle.Value);

            PlaySound(ev);

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

    private void SpawnGhostRole(SpecForcesType ev, EntityUid shuttle)
    {
        var spawns = new List<EntityCoordinates>();

        foreach (var (_, meta, xform) in EntityManager
                     .EntityQuery<SpawnPointComponent, MetaDataComponent, TransformComponent>(true))
        {
            if (meta.EntityPrototype?.ID != SpawnMarker)
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

        // TODO: Cvar
        var countExtra = _playerManager.PlayerCount switch
        {
            >= 60 => 7,
            >= 50 => 6,
            >= 40 => 5,
            >= 30 => 4,
            >= 20 => 3,
            >= 10 => 2,
            _ => 1
        };

        if (countExtra > 2 && _random.Prob(0.3f))
        {
            SpawnEntity(SFOfficer, _random.Pick(spawns));
        }

        switch (ev)
        {
            case SpecForcesType.ERT:
                SpawnEntity(ErtLeader, _random.Pick(spawns));
                SpawnEntity(ErtEngineer, _random.Pick(spawns));

                while (countExtra > 0)
                {
                    if (countExtra-- > 0)
                    {
                        SpawnEntity(ErtSecurity, _random.Pick(spawns));
                    }

                    if (countExtra-- > 0)
                    {
                        SpawnEntity(ErtMedical, _random.Pick(spawns));
                    }

                    if (countExtra-- > 0)
                    {
                        SpawnEntity(ErtJunitor, _random.Pick(spawns));
                    }
                }

                break;
            case SpecForcesType.RXBZZ:
                SpawnEntity(RxbzzLeader, _random.Pick(spawns));
                SpawnEntity(RxbzzFlamer, _random.Pick(spawns));
                while (countExtra > 0)
                {
                    if (countExtra-- > 0)
                    {
                        SpawnEntity(Rxbzz, _random.Pick(spawns));
                    }
                }

                break;
            case SpecForcesType.ERTAlpha:
                SpawnEntity(ErtAplhaLeader, _random.Pick(spawns));
                while (countExtra > 0)
                {
                    if (countExtra-- > 0)
                    {
                        SpawnEntity(ErtAplhaOperative, _random.Pick(spawns));
                    }
                }

                break;
            case SpecForcesType.ERTEpsilon:
                SpawnEntity(ErtEpsilonLeader, _random.Pick(spawns));

                while (countExtra > 0)
                {
                    if (countExtra-- > 0)
                    {
                        SpawnEntity(ErtEpsilonSecurity, _random.Pick(spawns));
                    }

                    if (countExtra-- > 0)
                    {
                        SpawnEntity(ErtEpsilonMedical, _random.Pick(spawns));
                    }

                    if (countExtra-- > 0)
                    {
                        SpawnEntity(ErtEpsilonMedical, _random.Pick(spawns));
                    }

                    if (countExtra-- > 0)
                    {
                        SpawnEntity(ErtEpsilonJunitor, _random.Pick(spawns));
                    }
                }

                break;
            case SpecForcesType.DeathSquad:
                SpawnEntity(SpestnazOfficer, _random.Pick(spawns));
                while (countExtra > 0)
                {
                    if (countExtra-- > 0)
                    {
                        SpawnEntity(Spestnaz, _random.Pick(spawns));
                    }
                }

                break;
            default:
                return;
        }
    }

    private EntityUid? SpawnShuttle(SpecForcesType ev)
    {
        var shuttleMap = _mapManager.CreateMap();
        var options = new MapLoadOptions()
        {
            LoadMap = true
        };

        if (!_map.TryLoad(shuttleMap,
                ev switch
                {
                    // todo: cvar
                    SpecForcesType.ERT => EtrShuttlePath,
                    SpecForcesType.ERTAlpha => ErtAplhaShuttlePath,
                    SpecForcesType.ERTEpsilon => ErtEpsilonShuttlePath,
                    SpecForcesType.RXBZZ => RxbzzShuttlePath,
                    SpecForcesType.DeathSquad => SpestnazShuttlePath,
                    _ => EtrShuttlePath
                },
                out var grids,
                options))
        {
            return null;
        }

        var mapGrid = grids.FirstOrNull();

        return mapGrid ?? null;
    }

    private void PlaySound(SpecForcesType ev)
    {
        var stations = _stationSystem.GetStations();
        if (stations.Count == 0)
        {
            return;
        }

        switch (ev)
        {
            case SpecForcesType.ERT:
                foreach (var station in stations)
                {
                    _chatSystem.DispatchStationAnnouncement(station,
                        Loc.GetString("spec-forces-system-ertcall-annonce"),
                        Loc.GetString("spec-forces-system-ertcall-title"),
                        false, _ertAnnounce
                    );
                }

                break;
            case SpecForcesType.ERTAlpha:
                foreach (var station in stations)
                {
                    _chatSystem.DispatchStationAnnouncement(station,
                        Loc.GetString("spec-forces-system-ertcall-annonce"),
                        Loc.GetString("spec-forces-system-ertAplha1call-title"),
                        false, _ertAnnounce
                    );
                }

                break;
            case SpecForcesType.RXBZZ:
                foreach (var station in stations)
                {
                    _chatSystem.DispatchStationAnnouncement(station,
                        Loc.GetString("spec-forces-system-RXBZZ-annonce"),
                        Loc.GetString("spec-forces-system-RXBZZ-title"),
                        true
                    );
                }

                break;
            default:
                return;
        }
    }

    private void OnRoundEnd(RoundEndTextAppendEvent ev)
    {
        foreach (var calledEvent in CalledEvents)
        {
            ev.AddLine(Loc.GetString("spec-forces-system-" + calledEvent.Event,
                ("time", calledEvent.RoundTime.ToString(@"hh\:mm\:ss")), ("who", calledEvent.WhoCalled)));
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

    [ValidatePrototypeId<EntityPrototype>] private const string SpawnMarker = "MarkerSpecforce";
    [ValidatePrototypeId<EntityPrototype>] private const string SFOfficer = "SpawnMobHumanSFOfficer";

    private const string EtrShuttlePath = "Maps/Shuttles/dart.yml";
    [ValidatePrototypeId<EntityPrototype>] private const string ErtLeader = "SpawnMobHumanERTLeaderEVAV2_1";
    [ValidatePrototypeId<EntityPrototype>] private const string ErtSecurity = "SpawnMobHumanERTSecurityEVAV2_1";
    [ValidatePrototypeId<EntityPrototype>] private const string ErtEngineer = "SpawnMobHumanERTEngineerEVAV2_1";
    [ValidatePrototypeId<EntityPrototype>] private const string ErtJunitor = "SpawnMobHumanERTJunitorEVAV2_1";
    [ValidatePrototypeId<EntityPrototype>] private const string ErtMedical = "SpawnMobHumanERTMedicalEVAV2_1";

    private const string ErtAplhaShuttlePath = "Maps/Backmen/Grids/NT-CC-Specnaz-013.yml";
    [ValidatePrototypeId<EntityPrototype>] private const string ErtAplhaLeader = "SpawnMobHumanERTLeaderAlpha1";
    [ValidatePrototypeId<EntityPrototype>] private const string ErtAplhaOperative = "SpawnMobHumanERTOperativeAlpha1";

    private const string ErtEpsilonShuttlePath = "Maps/Backmen/Grids/NT-DF-Kolibri-011.yml";
    [ValidatePrototypeId<EntityPrototype>] private const string ErtEpsilonLeader = "ReinforcementRadioMTFLeaderEgg";
    [ValidatePrototypeId<EntityPrototype>] private const string ErtEpsilonSecurity = "ReinforcementRadioMTFSecurityEgg";
    [ValidatePrototypeId<EntityPrototype>] private const string ErtEpsilonEngineer = "ReinforcementRadioMTFEngineerEgg";
    [ValidatePrototypeId<EntityPrototype>] private const string ErtEpsilonJunitor = "ReinforcementRadioMTFJunitorEgg";
    [ValidatePrototypeId<EntityPrototype>] private const string ErtEpsilonMedical = "ReinforcementRadioMTFMedicalEgg";

    private const string RxbzzShuttlePath = "Maps/Backmen/Grids/NT-CC-SRV-013.yml";
    [ValidatePrototypeId<EntityPrototype>] private const string RxbzzLeader = "MobHumanRXBZZLeader";
    [ValidatePrototypeId<EntityPrototype>] private const string Rxbzz = "SpawnMobHumanRXBZZ";
    [ValidatePrototypeId<EntityPrototype>] private const string RxbzzFlamer = "MobHumanRXBZZFlamer";

    private const string SpestnazShuttlePath = "Maps/Backmen/Grids/Invincible.yml";
    [ValidatePrototypeId<EntityPrototype>] private const string SpestnazOfficer = "SpawnMobHumanSpecialReAgentCOM";
    [ValidatePrototypeId<EntityPrototype>] private const string Spestnaz = "SpawnMobHumanSpecialReAgent";

    private readonly SoundSpecifier _ertAnnounce = new SoundPathSpecifier("/Audio/Corvax/Adminbuse/Yesert.ogg");

    [Dependency] private readonly IMapManager _mapManager = default!;

    //[Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly MapLoaderSystem _map = default!;
    [Dependency] private readonly GameTicker _gameTicker = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly ChatSystem _chatSystem = default!;
    [Dependency] private readonly StationSystem _stationSystem = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly ISerializationManager _serialization = default!;
    [Dependency] private readonly ActionsSystem _actions = default!;
}
