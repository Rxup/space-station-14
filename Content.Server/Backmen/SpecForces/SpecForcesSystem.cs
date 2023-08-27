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
using Content.Shared.Roles;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using Robust.Shared.Configuration;
using Content.Server.Players.PlayTimeTracking;
using Content.Shared.CCVar;
using Content.Server.Chat.Managers;
using Content.Server.Ghost.Roles;
using Content.Server.Ghost.Roles.Components;
using Content.Server.Mind.Components;
using Content.Server.RandomMetadata;
using Robust.Shared.Serialization.Manager;
using Content.Shared.Stealth.Components;
using Content.Shared.Inventory;
using Content.Shared.Radio.Components;
using Content.Server.Radio.EntitySystems;
using Content.Server.Administration.Managers;
using Content.Server.Backmen.RoleWhitelist;
using Content.Server.Mind;
using Content.Server.Players;
using Content.Shared.Actions;
using Content.Shared.Actions.ActionTypes;

namespace Content.Server.Backmen.SpecForces;

public sealed class SpecForcesSystem : EntitySystem
{
    // ReSharper disable once MemberCanBePrivate.Global
    [ViewVariables] public List<SpecForcesHistory> CalledEvents { get; private set; } = new List<SpecForcesHistory>();
    // ReSharper disable once MemberCanBePrivate.Global
    [ViewVariables] public TimeSpan LastUsedTime { get; private set; } = TimeSpan.Zero;

    private readonly TimeSpan _delayUsage = TimeSpan.FromMinutes(15);
    private readonly ReaderWriterLockSlim _callLock = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SpecForceComponent, MapInitEvent>(OnMapInit, after: new[] { typeof(RandomMetadataSystem) });
        SubscribeLocalEvent<RoundEndTextAppendEvent>(OnRoundEnd);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnCleanup);
        SubscribeLocalEvent<SpecForceComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<SpecForceComponent, TakeGhostRoleEvent>(OnSpecForceTake,
            before: new[] { typeof(GhostRoleSystem) });
    }

    private void OnStartup(EntityUid uid, SpecForceComponent component, ComponentStartup args)
    {
        if (component.ActionName == null ||
            !_prototypes.TryIndex<InstantActionPrototype>(component.ActionName, out var action))
        {
            return;
        }

        var netAction = new InstantAction(action);
        _action.AddAction(uid, netAction, null);
    }

    private void OnMapInit(EntityUid uid, SpecForceComponent component, MapInitEvent args)
    {
        if (component.Components != null)
        {
            foreach (var entry in component.Components.Values)
            {
                var comp = (Component) _serialization.CreateCopy(entry.Component, notNullableOverride: true);
                comp.Owner = uid;
                EntityManager.AddComponent(uid, comp, true);
            }
        }
    }

    private void OnSpecForceTake(EntityUid uid, SpecForceComponent component, ref TakeGhostRoleEvent args)
    {
        if (!_adminManager.IsAdmin(args.Player) && !IsAllowed(args.Player, component, out var reason))
        {
            args.TookRole = true;
            _chatManager.ChatMessageToOne(Shared.Chat.ChatChannel.Server, reason, "ОШИБКА: " + reason, default, false,
                args.Player.ConnectedClient, Color.Plum);
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

    public bool IsAllowed(IPlayerSession? player, SpecForceComponent job, [NotNullWhen(false)] out string? reason)
    {
        reason = null;

        if (job?.Requirements == null)
            return true;

        if (player == null)
            return true;

        if (!_cfg.GetCVar(CCVars.GameRoleTimers))
            return true;

        var playTimes = _tracking.GetTrackerTimes(player);

        var reasonBuilder = new StringBuilder();

        var first = true;
        foreach (var requirement in job.Requirements)
        {
            if (JobRequirements.TryRequirementMet(requirement, playTimes, out reason, _prototypes))
                continue;

            if (!first)
                reasonBuilder.Append('\n');
            first = false;

            reasonBuilder.AppendLine(reason);
        }

        if (_cfg.GetCVar(Shared.Backmen.CCVar.CCVars.WhitelistRolesEnabled) &&
            job.WhitelistRequired &&
            !_whitelistSystem.IsInWhitelist(player))
        {
            if (!first)
                reasonBuilder.Append('\n');
            first = false;

            reasonBuilder.AppendLine(Loc.GetString("playtime-deny-reason-not-whitelisted"));
        }

        reason = reasonBuilder.Length == 0 ? null : reasonBuilder.ToString();
        return reason == null;
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
            EntityManager.AddComponent(uid, comp, true);
        }

        EnsureComp<SpecForceComponent>(uid);
        if (spawnObj.TryGetComponent<GhostRoleComponent>(out var tplGhostRoleComponent))
        {
            var comp = (Component) _serialization.CreateCopy(tplGhostRoleComponent, notNullableOverride: true);
            comp.Owner = uid;
            EntityManager.AddComponent(uid, comp, true);
        }

        return uid;
    }

    private void SpawnGhostRole(SpecForcesType ev, EntityUid shuttle)
    {
        var spawns = new List<EntityCoordinates>();

        foreach (var (_, meta, xform) in EntityManager
                     .EntityQuery<SpawnPointComponent, MetaDataComponent, TransformComponent>(true))
        {
            if (meta.EntityPrototype?.ID != Spawner)
                continue;

            if (xform.ParentUid != shuttle)
                continue;

            spawns.Add(xform.Coordinates);
            break;
        }

        if (spawns.Count == 0)
        {
            spawns.Add(EntityManager.GetComponent<TransformComponent>(shuttle).Coordinates);
        }

        // TODO: Cvar
        var countExtra = _playerManager.PlayerCount switch
        {
            >= 40 => 4,
            >= 30 => 3,
            >= 20 => 2,
            >= 10 => 1,
            _ => 0
        };

        switch (ev)
        {
            case SpecForcesType.ERT:
                SpawnEntity(ErtLeader, _random.Pick(spawns));
                while (countExtra > 0)
                {
                    if (countExtra-- > 0)
                    {
                        SpawnEntity(ErtSecurity, _random.Pick(spawns));
                    }

                    if (countExtra-- > 0)
                    {
                        SpawnEntity(ErtEngineer, _random.Pick(spawns));
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
                SpawnEntity(countExtra == 0 ? Rxbzz : RxbzzLeader, _random.Pick(spawns));
                while (countExtra > 0)
                {
                    if (countExtra-- > 0)
                    {
                        SpawnEntity(Rxbzz, _random.Pick(spawns));
                    }
                }

                break;
            case SpecForcesType.DeathSquad:
                SpawnEntity(countExtra == 0 ? Spestnaz : SpestnazOfficer, _random.Pick(spawns));
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
                        true, _ertAnnounce
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

    [ValidatePrototypeId<EntityPrototype>] private const string Spawner = "SpawnSpecforce";

    private const string EtrShuttlePath = "Maps/Shuttles/dart.yml";
    [ValidatePrototypeId<EntityPrototype>] private const string ErtLeader = "SpawnMobHumanERTLeaderEVAV2.1";
    [ValidatePrototypeId<EntityPrototype>] private const string ErtSecurity = "SpawnMobHumanERTSecurityEVAV2.1";
    [ValidatePrototypeId<EntityPrototype>] private const string ErtEngineer = "SpawnMobHumanERTEngineerEVAV2.1";
    [ValidatePrototypeId<EntityPrototype>] private const string ErtJunitor = "SpawnMobHumanERTJunitorEVAV2.1";
    [ValidatePrototypeId<EntityPrototype>] private const string ErtMedical = "SpawnMobHumanERTMedicalEVAV2.1";

    private const string RxbzzShuttlePath = "Maps/Backmen/Grids/NT-CC-SRV-013.yml";
    [ValidatePrototypeId<EntityPrototype>] private const string RxbzzLeader = "SpawnMobHumanSFOfficer";
    [ValidatePrototypeId<EntityPrototype>] private const string Rxbzz = "SpawnMobHumanRXBZZ";

    private const string SpestnazShuttlePath = "Maps/Backmen/Grids/NT-CC-Specnaz-013.yml";
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
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly PlayTimeTrackingManager _tracking = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly IChatManager _chatManager = default!;
    [Dependency] private readonly ISerializationManager _serialization = default!;
    [Dependency] private readonly IAdminManager _adminManager = default!;
    [Dependency] private readonly SharedActionsSystem _action = default!;
    [Dependency] private readonly WhitelistSystem _whitelistSystem = default!;
}
