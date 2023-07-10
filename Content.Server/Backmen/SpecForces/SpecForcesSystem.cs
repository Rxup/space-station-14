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
using Content.Server.Mind;
using Content.Shared.Actions;
using Content.Shared.Actions.ActionTypes;

namespace Content.Server.Backmen.SpecForces;

public sealed class SpecForcesSystem : EntitySystem
{
    //public List<Mind.Mind> PlayersInEvent {get; private set;} = new List<Mind.Mind>();
    [ViewVariables] public List<SpecForcesHistory> CallendEvents { get; private set; } = new List<SpecForcesHistory>();
    [ViewVariables] public TimeSpan LastUsedTime { get; private set; } = TimeSpan.Zero;

    private readonly TimeSpan DelayUsesage = TimeSpan.FromMinutes(15);
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
        if (component.ActionName==null || !_prototypes.TryIndex<InstantActionPrototype>(component.ActionName, out var action))
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
        /*
        if (_inventory.TryGetSlotEntity(uid, "ears", out var ears) && TryComp<HeadsetComponent>(ears, out var earsComp))
        {
            earsComp.Enabled = false;
            //_headset.SetEnabled(ears.Value, false, earsComp);
        }
        */
    }

    private void OnSpecForceTake(EntityUid uid, SpecForceComponent component, ref TakeGhostRoleEvent args)
    {
        if (!_adminManager.IsAdmin(args.Player) && !IsAllowed(args.Player, component, out var reason))
        {
            args.TookRole = true;
            _callLock.EnterWriteLock();
            _chatManager.ChatMessageToOne(Shared.Chat.ChatChannel.Server, reason, "ОШИБКА: " + reason, default, false,
                args.Player.ConnectedClient, Color.Plum);

            //EntityManager.RemoveComponent<ActorComponent>(uid);
            var mind = EntityManager.EnsureComponent<MindContainerComponent>(uid);
            mind.Mind = new Content.Server.Mind.Mind(); // dummy

            var sess = args.Player;

            Robust.Shared.Timing.Timer.Spawn(0, () =>
            {
                try
                {
                    if (!uid.IsValid())
                    {
                        return;
                    }

                    mind.Mind = null;
                    if (EntityManager.TryGetComponent<GhostRoleComponent>(uid, out var ghostComp))
                    {
                        (ghostComp as dynamic).Taken = false;
                        //_ghostRoleSystem.RegisterGhostRole(ghostComp);
                    }

                    _ghostRoleSystem.CloseEui(sess);
                }
                finally
                {
                    _callLock.ExitWriteLock();
                }
            });
            return;
        }
/*
        if (_inventory.TryGetSlotEntity(uid, "ears", out var ears) && TryComp<HeadsetComponent>(ears, out var earsComp))
        {
            _headset.SetEnabled(ears.Value, true, earsComp);
        }
        */
    }

    public TimeSpan DelayTime
    {
        get
        {
            var ct = GameTicker.RoundDuration();
            var DelayTime = LastUsedTime + DelayUsesage;
            return ct > DelayTime ? TimeSpan.Zero : DelayTime - ct;
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

            var currentTime = GameTicker.RoundDuration();

            if (LastUsedTime + DelayUsesage > currentTime)
            {
                return false;
            }

            LastUsedTime = currentTime;

            CallendEvents.Add(new SpecForcesHistory { Event = ev, RoundTime = currentTime, WhoCalled = source });

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
        var uid = EntityManager.SpawnEntity(protoName, coordinates);
        EnsureComp<SpecForceComponent>(uid);
        return uid;
    }

    private void SpawnGhostRole(SpecForcesType ev, EntityUid shuttle)
    {
        var spawns = new List<EntityCoordinates>();

        foreach (var (_, meta, xform) in EntityManager
                     .EntityQuery<SpawnPointComponent, MetaDataComponent, TransformComponent>(true))
        {
            // TODO: marker
            //if (meta.EntityPrototype?.ID != "ERTMarker")
            //    continue;

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
                SpawnEntity(ERTLeader, _random.Pick(spawns));
                while (countExtra > 0)
                {
                    if (countExtra-- > 0)
                    {
                        SpawnEntity(ERTSecurity, _random.Pick(spawns));
                    }

                    if (countExtra-- > 0)
                    {
                        SpawnEntity(ERTEngineer, _random.Pick(spawns));
                    }

                    if (countExtra-- > 0)
                    {
                        SpawnEntity(ERTMedical, _random.Pick(spawns));
                    }

                    if (countExtra-- > 0)
                    {
                        SpawnEntity(ERTJunitor, _random.Pick(spawns));
                    }
                }

                break;
            case SpecForcesType.RXBZZ:
                SpawnEntity(countExtra == 0 ? RXBZZ : RXBZZLeader, _random.Pick(spawns));
                while (countExtra > 0)
                {
                    if (countExtra-- > 0)
                    {
                        SpawnEntity(RXBZZ, _random.Pick(spawns));
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
            LoadMap = true,
        };

        if (!_map.TryLoad(shuttleMap,
                ev switch
                {
                    // todo: cvar
                    SpecForcesType.ERT => ETRShuttlePath,
                    _ => ETRShuttlePath
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
                        true, ERTAnnounce
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
        foreach (var calledEvent in CallendEvents)
        {
            ev.AddLine(Loc.GetString("spec-forces-system-" + calledEvent.Event,
                ("time", calledEvent.RoundTime.ToString(@"hh\:mm\:ss")), ("who", calledEvent.WhoCalled)));
        }
    }

    private void OnCleanup(RoundRestartCleanupEvent ev)
    {
        CallendEvents.Clear();
        LastUsedTime = TimeSpan.Zero;

        if (_callLock.IsWriteLockHeld)
        {
            _callLock.ExitWriteLock();
        }
    }

    const string ETRShuttlePath = "Maps/Shuttles/dart.yml";
    const string ERTLeader = "MobHumanERTLeaderEVAV2.1";
    const string ERTSecurity = "MobHumanERTSecurityEVAV2.1";
    const string ERTEngineer = "MobHumanERTEngineerEVAV2.1";
    const string ERTJunitor = "MobHumanERTJunitorEVAV2.1";
    const string ERTMedical = "MobHumanERTMedicalEVAV2.1";

    const string RXBZZLeader = "MobHumanSFOfficer";
    const string RXBZZ = "MobHumanRXBZZ";


    const string SpestnazOfficer = "MobHumanSpecialReAgentCOM";
    const string Spestnaz = "MobHumanSpecialReAgent";

    public SoundSpecifier ERTAnnounce = new SoundPathSpecifier("/Audio/Corvax/Adminbuse/Yesert.ogg");

    [Dependency] private readonly IMapManager _mapManager = default!;

    //[Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly MapLoaderSystem _map = default!;
    [Dependency] private readonly GameTicker _gameTicker = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly ChatSystem _chatSystem = default!;
    [Dependency] private readonly StationSystem _stationSystem = default!;
    [Dependency] public readonly GameTicker GameTicker = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly PlayTimeTrackingManager _tracking = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly IChatManager _chatManager = default!;
    [Dependency] private readonly GhostRoleSystem _ghostRoleSystem = default!;
    [Dependency] private readonly ISerializationManager _serialization = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly HeadsetSystem _headset = default!;
    [Dependency] private readonly IAdminManager _adminManager = default!;
    [Dependency] private readonly MindSystem _mindSystem = default!;
    [Dependency] private readonly SharedActionsSystem _action = default!;
}
