using Content.Server.Administration.Logs;
using Content.Server.GameTicking;
using Content.Shared.GameTicking;
//using Content.Server.Mind;
using Robust.Server.GameObjects;
using Robust.Server.Maps;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using System.Linq;
using Content.Server.Spawners.Components;
using Robust.Shared.Random;
using Robust.Server.Player;
using Content.Server.Chat.Systems;
using Content.Server.Station.Systems;
using Robust.Shared.Utility;
using Robust.Shared.Audio;

namespace Content.Server.Backmen.SpecForces;

public sealed class SpecForcesSystem : EntitySystem
{
    //public List<Mind.Mind> PlayersInEvent {get; private set;} = new List<Mind.Mind>();
    public List<SpecForcesType> CallendEvents {get; private set;} = new List<SpecForcesType>();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RoundEndTextAppendEvent>(OnRoundEnd);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnCleanup);
    }
    public bool CallOps(SpecForcesType ev){
        if(_gameTicker.RunLevel != GameRunLevel.InRound){
            return false;
        }
        if(CallendEvents.Contains(ev)){
            return false;
        }
        CallendEvents.Add(ev);

        switch(ev){
            case SpecForcesType.ERT:
                CallETROps();
                break;
            default:
                return false;
        }

        return true;
    }
    private void CallETROps(){
        var shuttleMap = _mapManager.CreateMap();
        var options = new MapLoadOptions()
        {
            LoadMap = true,
        };

        if(!_map.TryLoad(shuttleMap, ETRShuttlePath, out var grids, options)){
            return;
        }

        var MapGrid = grids.FirstOrNull();

        if(MapGrid == null){
            return;
        }

        var spawns = new List<EntityCoordinates>();

        foreach (var (_, meta, xform) in EntityManager.EntityQuery<SpawnPointComponent, MetaDataComponent, TransformComponent>(true))
        {
            // TODO: marker
            //if (meta.EntityPrototype?.ID != "ERTMarker")
            //    continue;

            if (xform.ParentUid != MapGrid)
                continue;

            spawns.Add(xform.Coordinates);
            break;
        }
        if(spawns.Count == 0){
            spawns.Add(EntityManager.GetComponent<TransformComponent>(MapGrid.Value).Coordinates);
        }
        // TODO: Cvar
        var ERTCountExtra = _playerManager.PlayerCount switch {
            int x when x >= 40 => 4,
            int x when x >= 30 => 3,
            int x when x >= 20 => 2,
            int x when x >= 10 => 1,
            _ => 0
        };
        EntityManager.SpawnEntity(ERTLeader, _random.Pick(spawns));
        while(ERTCountExtra > 0){
            if(ERTCountExtra-- > 0){
                EntityManager.SpawnEntity(ERTSecurity, _random.Pick(spawns));
            }
            if(ERTCountExtra-- > 0){
                EntityManager.SpawnEntity(ERTEngineer, _random.Pick(spawns));
            }
            if(ERTCountExtra-- > 0){
                EntityManager.SpawnEntity(ERTMedical, _random.Pick(spawns));
            }
            if(ERTCountExtra-- > 0){
                EntityManager.SpawnEntity(ERTJunitor, _random.Pick(spawns));
            }
        }

        var station = _stationSystem.Stations.FirstOrNull();
        if(station != null){
            _chatSystem.DispatchStationAnnouncement(station.Value,
            Loc.GetString("spec-forces-system-ertcall-annonce"),
            Loc.GetString("spec-forces-system-ertcall-title"),
            true, ERTAnnounce
            );
        }
    }
    private void OnRoundEnd(RoundEndTextAppendEvent ev)
    {
        foreach(var CalledEevent in CallendEvents){
            ev.AddLine(Loc.GetString("spec-forces-system-"+CalledEevent));
        }
    }
    private void OnCleanup(RoundRestartCleanupEvent ev)
    {
        //PlayersInEvent.Clear();
        CallendEvents.Clear();
    }

    public const string ETRShuttlePath = "Maps/Shuttles/dart.yml";
    const string ERTLeader = "MobHumanERTLeaderEVAV2.1";
    const string ERTSecurity = "MobHumanERTSecurityEVAV2.1";
    const string ERTEngineer = "MobHumanERTEngineerEVAV2.1";
    const string ERTJunitor = "MobHumanERTJunitorEVAV2.1";
    const string ERTMedical = "MobHumanERTMedicalEVAV2.1";

    public SoundSpecifier ERTAnnounce = new SoundPathSpecifier("/Audio/Corvax/Adminbuse/Yesert.ogg");

    [Dependency] private readonly IMapManager _mapManager = default!;
    //[Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly MapLoaderSystem _map = default!;
    [Dependency] private readonly GameTicker _gameTicker = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly ChatSystem _chatSystem = default!;
    [Dependency] private readonly StationSystem _stationSystem = default!;
}
