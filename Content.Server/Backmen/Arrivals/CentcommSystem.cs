using System.Numerics;
using Content.Server.Chat.Systems;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Events;
using Content.Server.Popups;
using Content.Server.Power.EntitySystems;
using Content.Server.Salvage.Expeditions;
using Content.Server.Shuttle.Components;
using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Events;
using Content.Server.Shuttles.Systems;
using Content.Server.Station.Components;
using Content.Server.Station.Systems;
using Content.Shared.Backmen.Abilities;
using Content.Shared.Cargo.Components;
using Content.Shared.CCVar;
using Content.Shared.Emag.Components;
using Content.Shared.Emag.Systems;
using Content.Shared.GameTicking;
using Content.Shared.Shuttles.Components;
using Robust.Server.GameObjects;
using Robust.Server.Maps;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Utility;

namespace Content.Server.Backmen.Arrivals;

public sealed class FtlCentComAnnounce : EntityEventArgs
{
    public Entity<ShuttleComponent> Source { get; set; }
}

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
    [Dependency] private readonly TransformSystem _transformSystem = default!;
    [Dependency] private readonly IRobustRandom _robustRandom = default!;
    private ISawmill _sawmill = default!;


    public EntityUid CentComGrid { get; private set; } = EntityUid.Invalid;
    public MapId CentComMap { get; private set; } = MapId.Nullspace;
    public float ShuttleIndex { get; set; } = 0;

    public override void Initialize()
    {
        base.Initialize();
        _sawmill = Logger.GetSawmill("centcom");
        SubscribeLocalEvent<ActorComponent, CentcomFtlAction>(OnFtlActionUsed);
        SubscribeLocalEvent<PreGameMapLoad>(OnPreGameMapLoad, after: new[] { typeof(StationSystem) });
        SubscribeLocalEvent<RoundStartingEvent>(OnCentComInit, before: new[] { typeof(EmergencyShuttleSystem) });
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnCleanup);
        SubscribeLocalEvent<ShuttleConsoleComponent, GotEmaggedEvent>(OnShuttleConsoleEmaged);
        SubscribeLocalEvent<FTLCompletedEvent>(OnFTLCompleted);
        SubscribeLocalEvent<FtlCentComAnnounce>(OnFtlAnnounce);
        _cfg.OnValueChanged(CCVars.GridFill, OnGridFillChange);
    }

    private void OnFtlAnnounce(FtlCentComAnnounce ev)
    {
        if (!CentComGrid.IsValid())
        {
            return; // not loaded centcom
        }

        var transformQuery = EntityQueryEnumerator<TransformComponent, IFFConsoleComponent>();

        var shuttleName = "Неизвестный";

        while (transformQuery.MoveNext(out var owner, out var transformComponent, out var iff))
        {
            if (transformComponent.GridUid != ev.Source)
            {
                continue;
            }

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

        _audio.PlayPvs(SparkSound, ent);
        _popupSystem.PopupEntity(Loc.GetString("cloning-pod-component-upgrade-emag-requirement"), ent);
        args.Handled = true;
        EnsureComp<EmaggedComponent>(ent); // для обновления консоли нужно чтобы компонент был до вызыва RefreshShuttleConsoles
        _console.RefreshShuttleConsoles();
    }

    private void OnGridFillChange(bool obj)
    {
        if (obj)
        {
            EnsureCentcom(true);
        }
    }

    private void OnCleanup(RoundRestartCleanupEvent ev)
    {
        _sawmill.Info("OnCleanup");
        QueueDel(CentComGrid);
        CentComGrid = EntityUid.Invalid;

        if (_mapManager.MapExists(CentComMap))
            _mapManager.DeleteMap(CentComMap);

        CentComMap = MapId.Nullspace;
        ShuttleIndex = 0;
    }

    public void EnsureCentcom(bool force = false)
    {
        if (!_cfg.GetCVar(CCVars.GridFill) && !force)
        {
            return;
        }

        _sawmill.Info("EnsureCentcom");
        if (CentComGrid.IsValid())
        {
            return;
        }

        _sawmill.Info("Start load centcom");

        if (CentComMap == MapId.Nullspace)
        {
            CentComMap = _mapManager.CreateMap();
        }

        var ent = _gameTicker.LoadGameMap(
            _prototypeManager.Index<Maps.GameMapPrototype>(_robustRandom.Prob(0.35f) ? "CentCommv2" : "CentComm"), CentComMap, new MapLoadOptions()
            {
                LoadMap = false
            }, null).FirstOrNull(HasComp<BecomesStationComponent>);

        if (ent != null)
        {
            CentComGrid = ent.Value;
            _shuttle.AddFTLDestination(CentComGrid, true);
            DisableFtl();
        }
        else
        {
            _sawmill.Warning("No CentComm map found, skipping setup.");
        }
    }

    // ReSharper disable once MemberCanBePrivate.Global
    public void DisableFtl()
    {
        if (!CentComGrid.IsValid())
            return;
        var dest = EnsureComp<FTLDestinationComponent>(CentComGrid);
        dest.Whitelist ??= new();
        dest.Whitelist.RequireAll = false;
        dest.Whitelist.Components = new[] { "Emagged" };
        dest.Whitelist.UpdateRegistrations();
        _console.RefreshShuttleConsoles();
    }

    public void EnableFtl()
    {
        if (!CentComGrid.IsValid())
            return;
        var dest = EnsureComp<FTLDestinationComponent>(CentComGrid);
        dest.Whitelist = null;
        _console.RefreshShuttleConsoles();
    }

    private void OnCentComInit(RoundStartingEvent ev)
    {
        if (_gameTicker.CurrentPreset?.IsMiniGame ?? false) // no centcom in minigame
        {
            return;
        }

        EnsureCentcom();
    }

    private void OnPreGameMapLoad(PreGameMapLoad ev)
    {
        if (ev.GameMap.ID != "CentComm")
        {
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
            Deleted(centcomm.Entity))
        {
            _popup.PopupEntity(Loc.GetString("centcom-ftl-action-no-station"), args.Performer, args.Performer);
            return;
        }

        if (shuttle.MapID == centcomm.MapId)
        {
            _popup.PopupEntity(Loc.GetString("centcom-ftl-action-at-centcomm"), args.Performer, args.Performer);
            return;
        }

        if (!_shuttleSystem.CanFTL(shuttle.GridUid, out var reason))
        {
            _popup.PopupEntity(reason, args.Performer, args.Performer);
            return;
        }

        _shuttleSystem.FTLTravel(shuttle.GridUid.Value, comp, centcomm.Entity);
    }
}
