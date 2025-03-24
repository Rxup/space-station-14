using System.Linq;
using Content.Server.GameTicking.Events;
using Content.Server.Spawners.Components;
using Content.Server.Station.Components;
using Content.Server.Station.Systems;
using Content.Shared.GameTicking;
using Robust.Server.Player;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.Backmen.Arrivals.CentComm;

public sealed class CentCommSpawnSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly StationJobsSystem _stationJobsSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<StationCentCommDirectorComponent, CentCommEvent>(OnCentCommEvent);
        SubscribeLocalEvent<RoundStartingEvent>(OnStartRound);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundCleanup);
        SubscribeLocalEvent<StationCentCommDirectorComponent, ComponentStartup>(OnComponentStartup);
    }

    private void OnComponentStartup(Entity<StationCentCommDirectorComponent> ent, ref ComponentStartup args)
    {
#if DEBUG
        IsLowPop = false;
#endif
        var stationJobs = CompOrNull<StationJobsComponent>(ent.Owner);
        if (stationJobs == null)
            return;

        var stationDict = stationJobs.SetupAvailableJobs;
        stationDict.Clear();

        if (IsLowPop)
            return;

        var availableJobs = _playerManager.PlayerCount is >= 20 and < 40
            ? ent.Comp.SetupMedAvailableJobs
            : ent.Comp.SetupHighAvailableJobs;


        foreach (var job in ent.Comp.SetupHighAvailableJobs) //availableJobs)
        {
            stationDict[job.Key] = job.Value;
        }
    }

    private void OnStartRound(RoundStartingEvent msg, EntitySessionEventArgs args)
    {
        if (_playerManager.PlayerCount >= 20)
        {
            IsLowPop = false;
        }
    }

    private bool IsLowPop = true;

    private void OnRoundCleanup(RoundRestartCleanupEvent msg, EntitySessionEventArgs args)
    {
        IsLowPop = true;
    }

    private void OnCentCommEvent(Entity<StationCentCommDirectorComponent> ent, ref CentCommEvent args)
    {
        if (args.Handled)
            return;

        switch (args.EventId)
        {
            case CentComEventId.Noop:
                args.Handled = true;
                var point = FindSpawnPoint(args.Station);
                if (point == null)
                {
                    Log.Error($"Can't find spawn point for {EntityManager.ToPrettyString(args.Station)}");
                }
                break;

            case CentComEventId.AddWorker:
                args.Handled = true;

                AddWorker(args.Station);
                break;
            case CentComEventId.AddOperator:
                args.Handled = true;
                if (!IsLowPop)
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
            Log.Warning($"Can't find spawn point for {EntityManager.ToPrettyString(station)}");
            return;
        }

        Spawn(protoId, point.Value);
    }

    [ValidatePrototypeId<EntityPrototype>]
    private const string WorkerProto = "SpawnPointCMBKCCAssistant";

    private void AddWorker(EntityUid station) => SpawnEntity(station, WorkerProto);

    [ValidatePrototypeId<EntityPrototype>]
    private const string OperatorProto = "SpawnPointCMBKCCOperator";

    private void AddOperator(EntityUid station) => SpawnEntity(station, OperatorProto);

    [ValidatePrototypeId<EntityPrototype>]
    private const string SecurityProto = "SpawnPointCMBKCCSecOfficer";

    private void AddSecurity(EntityUid station) => SpawnEntity(station, SecurityProto);

    [ValidatePrototypeId<EntityPrototype>]
    private const string CargoProto = "SpawnPointCMBKCCCargo";

    private void AddCargo(EntityUid station) => SpawnEntity(station, CargoProto);

    private EntityCoordinates? FindSpawnPoint(EntityUid station)
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
