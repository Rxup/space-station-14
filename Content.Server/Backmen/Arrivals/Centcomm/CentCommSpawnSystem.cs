using System.Linq;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Events;
using Content.Server.Spawners.Components;
using Content.Server.Station.Components;
using Content.Server.Station.Systems;
using Content.Shared.GameTicking;
using Content.Shared.Station.Components;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.Backmen.Arrivals.CentComm;

public sealed partial class CentCommSpawnSystem : EntitySystem
{
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private GameTicker _gameTicker = default!;
    [Dependency] private StationJobsSystem _stationJobs = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<StationCentCommDirectorComponent, CentCommEvent>(OnCentCommEvent);
        SubscribeLocalEvent<StationInitializedEvent>(OnStationInitialized, before: [typeof(StationJobsSystem)]);
        SubscribeLocalEvent<RoundStartingEvent>(OnRoundStarting);
    }

    private void OnStationInitialized(StationInitializedEvent ev)
    {
        if (!TryComp<StationCentCommDirectorComponent>(ev.Station, out var director)
            || !TryComp<StationJobsComponent>(ev.Station, out var jobs))
        {
            return;
        }

        ConfigureJobs(ev.Station, director, jobs);
    }

    private void OnRoundStarting(RoundStartingEvent ev)
    {
        var query = EntityQueryEnumerator<StationCentCommDirectorComponent, StationJobsComponent>();
        while (query.MoveNext(out var uid, out var director, out var jobs))
        {
            ConfigureJobs(uid, director, jobs, syncJobList: true);
        }
    }

    private void ConfigureJobs(
        EntityUid station,
        StationCentCommDirectorComponent director,
        StationJobsComponent jobs,
        bool syncJobList = false)
    {
        ApplyJobConfiguration(station, director, jobs, _gameTicker.ReadyPlayerCount(), syncJobList);
    }

    internal void TriggerRoundStartingJobConfiguration()
    {
        OnRoundStarting(new RoundStartingEvent(_gameTicker.RoundId));
    }

    private void ApplyJobConfiguration(
        EntityUid station,
        StationCentCommDirectorComponent director,
        StationJobsComponent jobs,
        int playerCount,
        bool syncJobList)
    {
        director.isLowPop = playerCount < 20;

        var availableJobs = !director.isLowPop
            ? playerCount is >= 20 and < 40
                ? director.SetupMedAvailableJobs
                : director.SetupHighAvailableJobs
            : null;

        _stationJobs.SetSetupAvailableJobs(station, availableJobs, jobs, syncJobList);
    }

    private void OnCentCommEvent(Entity<StationCentCommDirectorComponent> ent, ref CentCommEvent args)
    {
        if (args.Handled)
            return;

        switch (args.EventId)
        {
            case CentComEventId.AddWorker:
                args.Handled = true;

                AddWorker(args.Station);
                break;
            case CentComEventId.AddOperator:
                args.Handled = true;
                if (!ent.Comp.isLowPop)
                    break;
                AddOperator(args.Station);
                break;
            case CentComEventId.AddSecurity:
                args.Handled = true;

                AddSecurity(args.Station);
                break;
            case CentComEventId.AddCargo:
                args.Handled = true;

                AddCargo(args.Station);
                break;
            default:
                return;
        }
    }

    private void SpawnEntity(EntityUid station, string protoId)
    {
        var point = FindSpawnPoint(station);
        if (point == null)
        {
            Log.Warning($"Can't find spawn point for {ToPrettyString(station)}");
            return;
        }

        Spawn(protoId, point.Value);
    }

    private readonly EntProtoId WorkerProto = "SpawnPointCMBKCCAssistant";

    private void AddWorker(EntityUid station) => SpawnEntity(station, WorkerProto);

    private readonly EntProtoId OperatorProto = "SpawnPointCMBKCCOperator";

    private void AddOperator(EntityUid station) => SpawnEntity(station, OperatorProto);

    private readonly EntProtoId SecurityProto = "SpawnPointCMBKCCSecOfficer";

    private void AddSecurity(EntityUid station) => SpawnEntity(station, SecurityProto);

    private readonly EntProtoId CargoProto = "SpawnPointCMBKCCCargo";

    private void AddCargo(EntityUid station) => SpawnEntity(station, CargoProto);

    public EntityCoordinates? FindSpawnPoint(EntityUid station)
    {
        var stationData = CompOrNull<StationDataComponent>(station);
        if (stationData == null)
            return null;

        var stationGrids = stationData.Grids;

        var result = new List<EntityCoordinates>();

        var q = EntityQueryEnumerator<SpawnPointComponent, TransformComponent>();
        while (q.MoveNext(out var uid, out var spawnPoint, out var transform))
        {
            if (spawnPoint.SpawnType != SpawnPointType.LateJoin || transform.GridUid == null)
                continue;
            if (!stationGrids.Contains(transform.GridUid.Value))
                continue;

            result.Add(transform.Coordinates);
        }

        return result.Count == 0 ? null : _random.Pick(result);
    }
}
