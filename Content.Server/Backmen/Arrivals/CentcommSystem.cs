using System.Numerics;
using Content.Server.Backmen.Arrivals.CentComm;
using Content.Server.Chat.Systems;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Events;
using Content.Server.Maps;
using Content.Server.Popups;
using Content.Server.Power.EntitySystems;
using Content.Server.Salvage.Expeditions;
using Content.Server.Shuttles;
using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Events;
using Content.Server.Shuttles.Systems;
using Content.Server.Station.Components;
using Content.Server.Station.Systems;
using Content.Shared.Backmen.Abilities;
using Content.Shared.Backmen.Arrivals;
using Content.Shared.Cargo.Components;
using Content.Shared.CCVar;
using Content.Shared.Emag.Components;
using Content.Shared.Emag.Systems;
using Content.Shared.GameTicking;
using Content.Shared.Random;
using Content.Shared.Random.Helpers;
using Content.Shared.Shuttles.Components;
using Content.Shared.Whitelist;
using Robust.Server.GameObjects;
using Robust.Server.Maps;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server.Backmen.Arrivals;

public sealed class CentcommSystem : EntitySystem
{
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly ShuttleSystem _shuttleSystem = default!;
    [Dependency] private readonly StationSystem _stationSystem = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly GameTicker _gameTicker = default!;
    [Dependency] private readonly ShuttleSystem _shuttle = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly ShuttleConsoleSystem _console = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly PopupSystem _popupSystem = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly MetaDataSystem _metaDataSystem = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly IGameTiming _gameTiming = default!;

    public EntityUid CentComGrid { get; private set; } = EntityUid.Invalid;
    public MapId CentComMap { get; private set; } = MapId.Nullspace;
    public EntityUid CentComMapUid { get; private set; } = EntityUid.Invalid;
    public float ShuttleIndex { get; set; } = 0;

    private WeightedRandomPrototype _stationCentComMapPool = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ActorComponent, CentcomFtlAction>(OnFtlActionUsed);
        SubscribeLocalEvent<PreGameMapLoad>(OnPreGameMapLoad, after: new[] { typeof(StationSystem) });
        SubscribeLocalEvent<RoundStartingEvent>(OnCentComInit, before: new[] { typeof(EmergencyShuttleSystem) });
        SubscribeLocalEvent<RoundEndedEvent>(OnCentComEndRound);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnCleanup);
        SubscribeLocalEvent<ShuttleConsoleComponent, GotEmaggedEvent>(OnShuttleConsoleEmaged);
        SubscribeLocalEvent<FTLCompletedEvent>(OnFTLCompleted);
        SubscribeLocalEvent<FtlCentComAnnounce>(OnFtlAnnounce);
        SubscribeLocalEvent<LoadingMapsEvent>(OnLoadingMaps);
        _cfg.OnValueChanged(CCVars.GridFill, OnGridFillChange);

        _stationCentComMapPool = _prototypeManager.Index<WeightedRandomPrototype>(StationCentComMapPool);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var curTime = _gameTiming.CurTime;

        var q = EntityQueryEnumerator<StationCentCommDirectorComponent, StationSpawningComponent>();
        while (q.MoveNext(out var stationUid, out var centcomDirector, out var stationSpawning))
        {
            if (!(centcomDirector.EventSchedule.Count > 0 && curTime >= centcomDirector.NextEventTick))
            {
                continue;
            }

            // Pop the event.
            var curEvent = centcomDirector.EventSchedule[0];
            centcomDirector.EventSchedule.RemoveAt(0);

            // Add the next event's offset to the ticker.
            if (centcomDirector.EventSchedule.Count > 0)
                centcomDirector.NextEventTick = curTime + centcomDirector.EventSchedule[0].timeOffset;

            Log.Info($"Running event: {curEvent}");

            var ev = new CentCommEvent(stationUid, curEvent.eventId);
            RaiseLocalEvent(stationUid, ev, true);
            if (!ev.Handled)
            {
                Log.Warning($"Running event: {curEvent} is not handled");
            }
        }
    }


    private void OnLoadingMaps(LoadingMapsEvent ev)
    {
        if (_gameTicker.CurrentPreset?.IsMiniGame ?? false)
            return;
        if (!_cfg.GetCVar(CCVars.GridFill))
            return;
        EnsureCentcom(true);
    }

    private void OnCentComEndRound(RoundEndedEvent ev)
    {
        if (CentComMapUid.IsValid() && _shuttleSystem.TryAddFTLDestination(CentComMap, true, out var ftl))
        {
            EnableFtl((CentComMapUid, ftl));
        }
    }

    private readonly HashSet<Entity<IFFConsoleComponent>> _iFfConsoleEntities = new();
    private void OnFtlAnnounce(FtlCentComAnnounce ev)
    {
        if (!CentComGrid.IsValid())
        {
            return; // not loaded centcom
        }

        var shuttleName = "Неизвестный";

        _iFfConsoleEntities.Clear();
        _lookup.GetGridEntities(ev.Source, _iFfConsoleEntities);

        foreach (var (owner,iff) in _iFfConsoleEntities)
        {
            var f = iff.AllowedFlags;
            if (f.HasFlag(IFFFlags.Hide))
            {
                continue;
            }

            var name = MetaData(ev.Source).EntityName;
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            shuttleName = name;
        }

        if (_gameTicker.RunLevel != GameRunLevel.InRound)
        {
            return; // Do not announce out of round
        }

        _chat.DispatchStationAnnouncement(CentComGrid,
            $"Внимание! Радары обнаружили {shuttleName} шаттл, входящий в космическое пространство объекта Центрального Командования!",
            "Радар", colorOverride: Color.Crimson);
    }

    private void OnFTLCompleted(ref FTLCompletedEvent ev)
    {
        if (!CentComGrid.IsValid())
        {
            return; // not loaded centcom
        }

        if (ev.MapUid != _mapManager.GetMapEntityId(CentComMap))
        {
            return; // not centcom
        }

        if (!TryComp<ShuttleComponent>(ev.Entity, out var shuttleComponent))
        {
            return;
        }

        QueueLocalEvent(new FtlCentComAnnounce
        {
            Source = (ev.Entity, shuttleComponent!)
        });
    }

    private static readonly SoundSpecifier SparkSound = new SoundCollectionSpecifier("sparks");

    [ValidatePrototypeId<EntityPrototype>]
    private const string StationShuttleConsole = "ComputerShuttle";

    private void OnShuttleConsoleEmaged(Entity<ShuttleConsoleComponent> ent, ref GotEmaggedEvent args)
    {
        if (Prototype(ent)?.ID != StationShuttleConsole)
        {
            return;
        }

        if (!this.IsPowered(ent, EntityManager))
            return;

        var shuttle = Transform(ent).GridUid;
        if (!HasComp<ShuttleComponent>(shuttle))
            return;

        if (!(HasComp<CargoShuttleComponent>(shuttle) || HasComp<SalvageShuttleComponent>(shuttle)))
            return;

        _audio.PlayPvs(SparkSound, ent);
        _popupSystem.PopupEntity(Loc.GetString("shuttle-console-component-upgrade-emag-requirement"), ent);
        args.Handled = true;
        EnsureComp<AllowFtlToCentComComponent>(shuttle.Value); // для обновления консоли нужно чтобы компонент был до вызыва RefreshShuttleConsoles
        _console.RefreshShuttleConsoles();
    }

    private void OnGridFillChange(bool obj)
    {
        if (obj)
        {
            EnsureCentcom();
        }
    }

    private void OnCleanup(RoundRestartCleanupEvent ev)
    {
        Log.Info("OnCleanup");
        QueueDel(CentComGrid);
        CentComGrid = EntityUid.Invalid;

        if (_mapManager.MapExists(CentComMap))
            _mapManager.DeleteMap(CentComMap);

        CentComMap = MapId.Nullspace;
        CentComMapUid = EntityUid.Invalid;
        ShuttleIndex = 0;
    }

    [ValidatePrototypeId<WeightedRandomPrototype>]
    private const string StationCentComMapPool = "DefaultCentcomPool";

    [ValidatePrototypeId<GameMapPrototype>]
    private const string StationCentComMapDefault = "CentComm";


    public void EnsureCentcom(bool force = false)
    {
        if (!force && (_gameTicker.RunLevel != GameRunLevel.InRound || !_cfg.GetCVar(CCVars.GridFill)))
            return;

        Log.Info("EnsureCentcom");
        if (CentComGrid.IsValid())
        {
            return;
        }

        Log.Info("Start load centcom");

        if (CentComMap == MapId.Nullspace)
        {
            CentComMap = _mapManager.CreateMap();
        }

        CentComMapUid = _mapManager.GetMapEntityId(CentComMap);

        var mapId = _stationCentComMapPool.Pick(_random);
        if (!_prototypeManager.TryIndex<GameMapPrototype>(mapId, out var map))
        {
            mapId = StationCentComMapDefault;
            map = _prototypeManager.Index<GameMapPrototype>(StationCentComMapDefault);
        }


        var ent = _gameTicker.LoadGameMap(
            map, CentComMap, new MapLoadOptions()
            {
                LoadMap = false
            }, "Central Command").FirstOrNull(HasComp<BecomesStationComponent>);

        _metaDataSystem.SetEntityName(_mapManager.GetMapEntityId(CentComMap), Loc.GetString("map-name-centcomm"));

        if (ent == null)
        {
            Log.Warning("No CentComm map found, skipping setup.");
            return;
        }

        CentComGrid = ent.Value;
        if (_shuttle.TryAddFTLDestination(CentComMap, true, false, false, out var ftl))
        {
            ftl.RequireCoordinateDisk = false;
            DisableFtl((CentComMapUid, ftl));
        }

        var q = EntityQueryEnumerator<StationCentcommComponent>();
        while (q.MoveNext(out var station))
        {
            station.MapEntity = CentComMapUid;
            station.Entity = CentComGrid;
            station.ShuttleIndex = ShuttleIndex;
        }
    }

    // ReSharper disable once MemberCanBePrivate.Global
    public void DisableFtl(Entity<FTLDestinationComponent?> ent)
    {
        if(!Resolve(ent, ref ent.Comp))
            return;

        var d = new EntityWhitelist
        {
            RequireAll = false,
            Components = new[] { "AllowFtlToCentCom" }
        };

        ent.Comp.RequireCoordinateDisk = false;
        ent.Comp.BeaconsOnly = false;

        _shuttle.SetFTLWhitelist(ent, d);
    }

    public void EnableFtl(Entity<FTLDestinationComponent?> ent)
    {
        if(!Resolve(ent, ref ent.Comp))
            return;
        ent.Comp.RequireCoordinateDisk = false;
        ent.Comp.BeaconsOnly = false;

        _shuttle.SetFTLWhitelist(ent, null);
    }

    private void OnCentComInit(RoundStartingEvent ev)
    {
        if (_gameTicker.CurrentPreset?.IsMiniGame ?? false)
            return;
        if (!_cfg.GetCVar(CCVars.GridFill))
            return;

        EnsureCentcom(true);
    }

    private void OnPreGameMapLoad(PreGameMapLoad ev)
    {
        if (!_stationCentComMapPool.Weights.ContainsKey(ev.GameMap.ID))
        {
            if(ev.GameMap.ID != StationCentComMapDefault) // fallback
                return;
        }

        ev.Options.Offset = new Vector2(0, 0);
    }

    private void OnFtlActionUsed(EntityUid uid, ActorComponent component, CentcomFtlAction args)
    {
        var grid = Transform(args.Performer);
        if (grid.GridUid == null)
        {
            return;
        }

        if (!TryComp<PilotComponent>(args.Performer, out var pilotComponent) || pilotComponent.Console == null)
        {
            _popup.PopupEntity(Loc.GetString("centcom-ftl-action-no-pilot"), args.Performer, args.Performer);
            return;
        }

        TransformComponent shuttle;

        if (TryComp<DroneConsoleComponent>(pilotComponent.Console, out var droneConsoleComponent) &&
            droneConsoleComponent.Entity != null)
        {
            shuttle = Transform(droneConsoleComponent.Entity.Value);
        }
        else
        {
            shuttle = grid;
        }


        if (!TryComp<ShuttleComponent>(shuttle.GridUid, out var comp) || HasComp<FTLComponent>(shuttle.GridUid) || (
                HasComp<BecomesStationComponent>(shuttle.GridUid) &&
                !(
                    HasComp<SalvageShuttleComponent>(shuttle.GridUid) ||
                    HasComp<CargoShuttleComponent>(shuttle.GridUid)
                )
            ))
        {
            return;
        }

        var stationUid = _stationSystem.GetStations().FirstOrNull(HasComp<StationCentcommComponent>);

        if (!TryComp<StationCentcommComponent>(stationUid, out var centcomm) ||
             centcomm.Entity == null || !centcomm.Entity.Value.IsValid() || Deleted(centcomm.Entity))
        {
            _popup.PopupEntity(Loc.GetString("centcom-ftl-action-no-station"), args.Performer, args.Performer);
            return;
        }
/*
        if (shuttle.MapUid == centcomm.MapEntity)
        {
            _popup.PopupEntity(Loc.GetString("centcom-ftl-action-at-centcomm"), args.Performer, args.Performer);
            return;
        }
*/
        if (!_shuttleSystem.CanFTL(shuttle.GridUid.Value, out var reason))
        {
            _popup.PopupEntity(reason, args.Performer, args.Performer);
            return;
        }

        _shuttleSystem.FTLToDock(shuttle.GridUid.Value, comp, centcomm.Entity.Value);
    }
}
