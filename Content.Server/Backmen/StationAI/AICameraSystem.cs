using Content.Server.Interaction;
using Content.Server.SurveillanceCamera;
using Content.Shared.Backmen.StationAI.Components;
using Content.Shared.Eye.Blinding.Components;
using Robust.Shared.CPUJob.JobQueues.Queues;
using Robust.Shared.Map;

namespace Content.Server.Backmen.StationAI;

public sealed class AICameraSystem : EntitySystem
{
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
     [Dependency] private readonly SharedTransformSystem _transform = default!;
     [Dependency] private readonly InteractionSystem _interaction = default!;
     [Dependency] private readonly SurveillanceCameraSystem _cameraSystem = default!;



     private const double MoverJobTime = 0.005;
     private readonly JobQueue _moveJobQueue = new(MoverJobTime);

     public const float CameraEyeRange = 10f;

     public override void Initialize()
     {
         base.Initialize();

         SubscribeLocalEvent<AICameraComponent, ComponentStartup>(HandleCameraStartup);

         SubscribeLocalEvent<AIEyeComponent, MoveEvent>(OnEyeMove);
         SubscribeLocalEvent<AICameraComponent, SurveillanceCameraDeactivateEvent>(OnActiveCameraDisable);
         SubscribeLocalEvent<AICameraComponent, EntityTerminatingEvent>(OnRemove);
     }

     private void OnRemove(Entity<AICameraComponent> ent, ref EntityTerminatingEvent args)
     {
         OnCameraOffline(ent);
     }


/*
     private void OnShootCam(Entity<AIEyeComponent> ent, ref AIEyeCampShootActionEvent args)
     {
         if (!ent.Comp.AiCore.HasValue || ent.Comp.Camera == null)
             return;

         var camMapPos = _transform.GetMapCoordinates(ent.Comp.Camera.Value);
         var camPos = Transform(ent.Comp.Camera.Value).Coordinates;
         if (args.Target.GetGridUid(EntityManager) != Transform(ent.Comp.AiCore.Value).GridUid)
             return;

         args.Handled = true;
         var targetPos = args.Target.ToMap(EntityManager, _transform);

         var ammo = Spawn(BulletDisabler, camPos);
         _gun.ShootProjectile(ammo, targetPos.Position - camMapPos.Position, Vector2.One, ent.Comp.Camera.Value, args.Performer);
         _audio.PlayPvs("/Audio/Weapons/Guns/Gunshots/taser2.ogg", ent.Comp.Camera.Value);
     }

     private void OnMoveToCam(Entity<AIEyeComponent> ent, ref EyeMoveToCam args)
     {
         if (!TryGetEntity(args.Uid, out var uid) || !ent.Comp.AiCore.HasValue)
             return;
         var camPos = Transform(uid.Value);
         if (Transform(uid.Value).GridUid != Transform(ent.Comp.AiCore.Value).GridUid)
             return;

         if (!TryComp<SurveillanceCameraComponent>(uid, out var camera))
             return;

         if (!camera.Active)
         {
             _popup.PopupCursor("камера не работает!", ent, PopupType.LargeCaution);
             return;
         }
         _transform.SetCoordinates(ent, camPos.Coordinates);
         _transform.AttachToGridOrMap(ent);
     }
*/
     private void OnActiveCameraDisable(Entity<AICameraComponent> ent, ref SurveillanceCameraDeactivateEvent args)
     {
         OnCameraOffline(ent);
     }

     private void OnCameraOffline(Entity<AICameraComponent> ent)
     {
         foreach (var viewer in ent.Comp.ActiveViewers)
         {
             if (TerminatingOrDeleted(viewer) || !TryComp<AIEyeComponent>(viewer, out var aiEyeComponent))
             {
                 continue;
             }

             RemoveActiveCamera((viewer, aiEyeComponent));
             EnsureComp<TemporaryBlindnessComponent>(viewer);
         }
         ent.Comp.ActiveViewers.Clear();
     }

     public override void Update(float frameTime)
     {
         base.Update(frameTime);

         _moveJobQueue.Process();
     }

     private void OnEyeMove(Entity<AIEyeComponent> ent, ref MoveEvent args)
     {
         if (ent.Comp.IsProcessingMoveEvent)
             return;

         ent.Comp.IsProcessingMoveEvent = true;

         var job = new AiEyeMover(this, _lookup, _transform, MoverJobTime)
         {
             Eye = ent,
             //OldPosition = args.OldPosition,
             NewPosition = args.NewPosition
         };

         _moveJobQueue.EnqueueJob(job);
     }

     public bool IsCameraActive(Entity<AIEyeComponent> eye, MapCoordinates eyeCoordinates)
     {
         if (!eye.Comp.Camera.HasValue)
             return false;


         return _interaction.InRangeUnobstructed(eyeCoordinates, eye.Comp.Camera.Value, CameraEyeRange);
     }

     public void RemoveActiveCamera(Entity<AIEyeComponent> eye)
     {
         if (!eye.Comp.Camera.HasValue)
         {
             return;
         }

         if (!TryComp<SurveillanceCameraComponent>(eye.Comp.Camera.Value, out var camera))
         {
             return;
         }
         var v = camera.ActiveViewers;
         v.Remove(eye);
         _cameraSystem.UpdateVisuals(eye.Comp.Camera.Value, camera);
         EnsureComp<AICameraComponent>(eye.Comp.Camera.Value).ActiveViewers.Remove(eye);
         eye.Comp.Camera = null;
         Dirty(eye,eye.Comp);
     }
     private void ChangeActiveCamera(Entity<AIEyeComponent> eye, EntityUid camUid, SurveillanceCameraComponent? cameraComponent = null)
     {
         if (!Resolve(camUid, ref cameraComponent, false))
         {
             return;
         }

         if (eye.Comp.Camera.HasValue && eye.Comp.Camera != camUid)
         {
             RemoveActiveCamera(eye);
         }

         if (eye.Comp.Camera.HasValue)
         {
             return;
         }

         var v = cameraComponent.ActiveViewers;

         eye.Comp.Camera = camUid;
         Dirty(eye,eye.Comp);
         v.Add(eye);
         _cameraSystem.UpdateVisuals(camUid, cameraComponent);
         EnsureComp<AICameraComponent>(camUid).ActiveViewers.Add(eye);
     }

     public void HandleMove(Entity<AIEyeComponent> eye, MapCoordinates eyeCoordinates, HashSet<Entity<SurveillanceCameraComponent>> cameraComponents)
     {
         foreach (var cameraComponent in cameraComponents)
         {
             var v = cameraComponent.Comp.ActiveViewers;
             if(!cameraComponent.Comp.Active)
                 continue;

             if(!_interaction.InRangeUnobstructed(eyeCoordinates, cameraComponent, CameraEyeRange))
                 continue;

             ChangeActiveCamera(eye, cameraComponent, cameraComponent.Comp);
             return;
         }

         if (eye.Comp.Camera.HasValue)
         {
             RemoveActiveCamera(eye);
         }
     }

     private void HandleCameraStartup(EntityUid uid, AICameraComponent component, ComponentStartup args)
     {
         if (!TryComp<SurveillanceCameraComponent>(uid, out var camera))
             return;

         component.CameraName = camera.CameraId;
         component.CameraCategories = camera.AvailableNetworks;
         component.Enabled = true;

         Dirty(uid, component);
     }
}
