﻿using System.Threading;
using System.Threading.Tasks;
using Content.Server.Backmen.StationAI.Systems;
using Content.Server.SurveillanceCamera;
using Content.Shared.Backmen.StationAI;
using Content.Shared.Tag;
using Robust.Server.GameObjects;
using Robust.Shared.CPUJob.JobQueues;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Timing;

namespace Content.Server.Backmen.StationAI;

public sealed class AiEyeMover : Job<object>
{
    private readonly AICameraSystem _cameraSystem;
    private readonly EntityLookupSystem _lookup;
    private readonly SharedTransformSystem _transform;
    private readonly EntityManager _entityManager;
    private readonly MapSystem _map;
    private readonly TagSystem _tag;

    public AiEyeMover(EntityManager entityManager, AICameraSystem cameraSystem, EntityLookupSystem lookup, SharedTransformSystem transform, MapSystem map, TagSystem tag, double maxTime, CancellationToken cancellation = default) : base(maxTime, cancellation)
    {
        _cameraSystem = cameraSystem;
        _lookup = lookup;
        _transform = transform;
        _entityManager = entityManager;
        _map = map;
        _tag = tag;
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


            var gridUid = _transform.GetGrid(NewPosition);

            if (
                gridUid == null ||
                _transform.GetGrid(_transform.GetMoverCoordinates(core)) != gridUid ||
                !_entityManager.TryGetComponent<MapGridComponent>(gridUid, out var grid))
            {
                _entityManager.QueueDeleteEntity(Eye);
                return null;
            }

            var mapPos = _transform.ToMapCoordinates(NewPosition);

            // cache
            if (_cameraSystem.IsCameraActive(Eye, mapPos))
                return null;

            foreach (var uid in _map.GetAnchoredEntities(gridUid.Value, grid, NewPosition))
            {
                if (_tag.HasAnyTag(uid, "Wall","Window","Airlock","GlassAirlock"))
                    return null;
            }

            await WaitAsyncTask(Task.Run(() =>
                _lookup.GetEntitiesInRange(mapPos, SharedStationAISystem.CameraEyeRange, _cameraComponents, LookupFlags.Sensors)));

            _cameraSystem.HandleMove(Eye, mapPos, _cameraComponents);
        }
        finally
        {
            Eye.Comp.IsProcessingMoveEvent = false;
        }
        return null;
    }
}
