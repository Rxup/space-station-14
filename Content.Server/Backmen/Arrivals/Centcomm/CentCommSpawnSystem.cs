using Content.Server.Spawners.Components;
using Content.Server.Station.Components;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.Backmen.Arrivals.CentComm;

public sealed class CentCommSpawnSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<StationCentCommDirectorComponent, CentCommEvent>(OnCentCommEvent);
    }

    private void OnCentCommEvent(Entity<StationCentCommDirectorComponent> ent, ref CentCommEvent args)
    {
        if(args.Handled)
            return;

        switch (args.EventId)
        {
            case CentComEventId.AddWorker:
                args.Handled = true;

                AddWorker(args.Station);
                break;
            case CentComEventId.AddOperator:
                args.Handled = true;

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

    [ValidatePrototypeId<EntityPrototype>]
    private const string WorkerProto = "SpawnPointCMBKCCAssistant";
    private void AddWorker(EntityUid station)
    {
        var point = FindSpawnPoint(station);
        if (point == null)
        {
            Log.Warning($"Can't find spawn point for {station}");
            return;
        }
        Spawn(WorkerProto, point.Value);
    }
    [ValidatePrototypeId<EntityPrototype>]
    private const string OperatorProto = "SpawnPointCMBKCCOperator";
    private void AddOperator(EntityUid station)
    {
        var point = FindSpawnPoint(station);
        if (point == null)
        {
            Log.Warning($"Can't find spawn point for {station}");
            return;
        }
        Spawn(OperatorProto, point.Value);
    }
    [ValidatePrototypeId<EntityPrototype>]
    private const string SecurityProto = "SpawnPointCMBKCCSecOfficer";
    private void AddSecurity(EntityUid station)
    {
        var point = FindSpawnPoint(station);
        if (point == null)
        {
            Log.Warning($"Can't find spawn point for {station}");
            return;
        }
        Spawn(SecurityProto, point.Value);
    }
    [ValidatePrototypeId<EntityPrototype>]
    private const string CargoProto = "SpawnPointCMBKCCCargo";
    private void AddCargo(EntityUid station)
    {
        var point = FindSpawnPoint(station);
        if (point == null)
        {
            Log.Warning($"Can't find spawn point for {station}");
            return;
        }
        Spawn(CargoProto, point.Value);
    }

    private EntityCoordinates? FindSpawnPoint(EntityUid station)
    {
        var stationData = CompOrNull<StationDataComponent>(station);
        if (stationData == null)
            return null;

        var stationGrids = stationData.Grids;

        var result = new List<EntityCoordinates>();

        var q = EntityQueryEnumerator<SpawnPointComponent,TransformComponent>();
        while (q.MoveNext(out var uid, out var spawnPoint, out var transform))
        {
            if(spawnPoint.SpawnType != SpawnPointType.LateJoin || transform.GridUid == null)
                continue;
            if(!stationGrids.Contains(transform.GridUid.Value))
                continue;

            result.Add(transform.Coordinates);
        }

        return result.Count == 0 ? null : _random.Pick(result);
    }
}
