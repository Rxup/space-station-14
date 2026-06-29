using System.Threading;
using System.Threading.Tasks;
using Content.Shared.Backmen.StationAI.Components;
using Content.Shared.SurveillanceCamera.Components;
using Robust.Shared.CPUJob.JobQueues;
using Robust.Shared.Map;

namespace Content.Server.Backmen.StationAI;

public sealed class AiEyeMover : Job<object>
 {
     private readonly AICameraSystem _cameraSystem;
     private readonly EntityLookupSystem _lookup;
     private readonly SharedTransformSystem _transform;

     public AiEyeMover(AICameraSystem cameraSystem, EntityLookupSystem lookup, SharedTransformSystem transform, double maxTime, CancellationToken cancellation = default) : base(maxTime, cancellation)
     {
         _cameraSystem = cameraSystem;
         _lookup = lookup;
         _transform = transform;
     }

     public Entity<AIEyeComponent> Eye { get; set; }
     public EntityCoordinates NewPosition { get; set; }


     private readonly HashSet<Entity<SurveillanceCameraComponent>> _cameraComponents = new();

     protected override async Task<object?> Process()
     {
         try
         {
             var mapPos = _transform.ToMapCoordinates(NewPosition, false);

             await WaitAsyncTask(Task.Run(() =>
                 _lookup.GetEntitiesInRange(mapPos, AICameraSystem.CameraEyeRange, _cameraComponents, LookupFlags.Sensors)));

             _cameraSystem.HandleMove(Eye, mapPos, _cameraComponents);
         }
         finally
         {
             Eye.Comp.IsProcessingMoveEvent = false;
         }
         return null;
     }
 }
