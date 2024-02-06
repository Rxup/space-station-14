using System.Threading;
using System.Threading.Tasks;
using Content.Server.Backmen.StationAI.Systems;
using Content.Server.SurveillanceCamera;
using Content.Shared.Backmen.StationAI;
using Robust.Shared.CPUJob.JobQueues;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Server.Backmen.StationAI;

public sealed class AiEyeMover : Job<object>
{
    private readonly AICameraSystem _cameraSystem;
    private readonly EntityLookupSystem _lookup;
    private readonly SharedTransformSystem _transform;
    private readonly EntityManager _entityManager;

    public AiEyeMover(EntityManager entityManager, AICameraSystem cameraSystem, EntityLookupSystem lookup, SharedTransformSystem transform, double maxTime, CancellationToken cancellation = default) : base(maxTime, cancellation)
    {
        _cameraSystem = cameraSystem;
        _lookup = lookup;
        _transform = transform;
        _entityManager = entityManager;
    }

    public AiEyeMover(EntityManager entityManager, AICameraSystem cameraSystem, EntityLookupSystem lookup, SharedTransformSystem transform, double maxTime, IStopwatch stopwatch, CancellationToken cancellation = default) : base(maxTime, stopwatch, cancellation)
    {
        _cameraSystem = cameraSystem;
        _lookup = lookup;
        _transform = transform;
        _entityManager = entityManager;
    }

    public Entity<AIEyeComponent> Eye { get; set; }
    public EntityCoordinates NewPosition { get; set; }


    private readonly HashSet<Entity<SurveillanceCameraComponent>> _cameraComponents = new();

    protected override async Task<object?> Process()
    {
        try
        {
            if (!Eye.Comp.AiCore.HasValue)
            {
                _entityManager.QueueDeleteEntity(Eye);
                return null;
            }

            var core = Eye.Comp.AiCore.Value;

            var gridUid = NewPosition.GetGridUid(_entityManager);

            if (gridUid == null || _transform.GetMoverCoordinates(core).GetGridUid(_entityManager) != gridUid)
            {
                _entityManager.QueueDeleteEntity(Eye);
                return null;
            }

            // cache
            if (_cameraSystem.IsCameraActive(Eye))
                return null;

            var mapPos = NewPosition.ToMap(_entityManager, _transform);

            await WaitAsyncTask(Task.Run(() =>
                _lookup.GetEntitiesInRange(mapPos, AICameraSystem.CameraEyeRange, _cameraComponents, LookupFlags.Sensors)));

            _cameraSystem.HandleMove(Eye, _cameraComponents);
        }
        finally
        {
            Eye.Comp.IsProcessingMoveEvent = false;
        }
        return null;
    }
}
