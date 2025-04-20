using System.Numerics;
using Content.Server.Interaction;
using Content.Server.Popups;
using Content.Server.SurveillanceCamera;
using Content.Server.Weapons.Ranged.Systems;
using Content.Shared.Backmen.StationAI;
using Content.Shared.Backmen.StationAI.Components;
using Content.Shared.Eye.Blinding.Components;
using Content.Shared.Popups;
using Content.Shared.Silicons.StationAi;
using Content.Shared.Tag;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.CPUJob.JobQueues.Queues;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server.Backmen.StationAI;

public sealed class AICameraSystem : EntitySystem
{
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly InteractionSystem _interaction = default!;
    [Dependency] private readonly SurveillanceCameraSystem _cameraSystem = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly GunSystem _gun = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly UserInterfaceSystem _uiSystem = default!;
    [Dependency] private readonly SharedStationAiSystem _stationAi = default!;


     private const double MoverJobTime = 0.005;
     private readonly JobQueue _moveJobQueue = new(MoverJobTime);

     public const float CameraEyeRange = 10f;

     public override void Initialize()
     {
         base.Initialize();

         SubscribeLocalEvent<AICameraComponent, ComponentStartup>(HandleCameraStartup);

         SubscribeLocalEvent<AIEyeComponent, ComponentGetStateAttemptEvent>(OnIsEyeOwner);
         SubscribeLocalEvent<AIEyeComponent, MoveEvent>(OnEyeMove);
         SubscribeLocalEvent<AICameraComponent, SurveillanceCameraDeactivateEvent>(OnActiveCameraDisable);
         SubscribeLocalEvent<AICameraComponent, EntityTerminatingEvent>(OnRemove);

         SubscribeLocalEvent<StationAiHeldComponent, AIEyeCampShootActionEvent>(OnShoot);
         SubscribeLocalEvent<StationAiHeldComponent, AIEyeCampActionEvent>(OnOpenCamUi);
         Subs.BuiEvents<StationAiHeldComponent>(AICameraListUiKey.Key,
             sub =>
             {
                 sub.Event<EyeMoveToCam>(OnMoveToCam);
                 sub.Event<EyeCamRequest>(UpdateCams);
             });
     }

     private void OnIsEyeOwner(Entity<AIEyeComponent> ent, ref ComponentGetStateAttemptEvent args)
     {
         if (args.Player?.AttachedEntity is not { } plr ||
             !_stationAi.TryGetCore(plr, out var core) ||
             core.Comp?.RemoteEntity == null ||
             core.Comp.RemoteEntity != ent)
             args.Cancelled = true;

     }

     private void UpdateCams(Entity<StationAiHeldComponent> ent, ref EyeCamRequest args)
     {
         if (
             !_stationAi.TryGetCore(ent, out var core) ||
             core.Comp?.RemoteEntity == null ||
             !TryComp<AIEyeComponent>(core.Comp.RemoteEntity, out var aiEye)
             )
             return;

         aiEye.FollowsCameras.Clear();

         var pos = Transform(ent).GridUid;
         var cams = EntityQueryEnumerator<SurveillanceCameraComponent, TransformComponent>();
         while (cams.MoveNext(out var camUid, out var cam, out var transformComponent))
         {
             if(transformComponent.GridUid != pos)
                 continue;
             aiEye.FollowsCameras.Add((GetNetEntity(camUid), GetNetCoordinates(transformComponent.Coordinates)));
         }
         Dirty(core.Comp.RemoteEntity.Value, aiEye);
     }

     private void OnOpenCamUi(Entity<StationAiHeldComponent> ent, ref AIEyeCampActionEvent args)
     {
         //AIEye
         _uiSystem.TryToggleUi(ent.Owner, AICameraListUiKey.Key, ent);
     }

     private void OnMoveToCam(Entity<StationAiHeldComponent> ent, ref EyeMoveToCam args)
     {
         if (!_stationAi.TryGetCore(ent, out var core) || core.Comp?.RemoteEntity == null)
             return;

         if (
             !TryGetEntity(args.Uid, out var uid) ||
             TerminatingOrDeleted(core.Comp.RemoteEntity) ||
             !HasComp<AIEyeComponent>(core.Comp.RemoteEntity)
             )
             return;

         var camPos = Transform(uid.Value);
         if (Transform(uid.Value).GridUid != Transform(ent).GridUid)
             return;

         if (!TryComp<SurveillanceCameraComponent>(uid, out var camera))
             return;

         if (!camera.Active)
         {
             _popup.PopupCursor("камера не работает!", ent, PopupType.LargeCaution);
             return;
         }
         _transform.SetCoordinates(core.Comp.RemoteEntity.Value, camPos.Coordinates);
         _transform.AttachToGridOrMap(core.Comp.RemoteEntity.Value);
     }

     [ValidatePrototypeId<EntityPrototype>]
     private const string BulletDisabler = "BulletDisabler";
     private void OnShoot(Entity<StationAiHeldComponent> ent, ref AIEyeCampShootActionEvent args)
     {
         if (!_stationAi.TryGetCore(ent.Owner, out var core) || core.Comp?.RemoteEntity == null)
             return;

         if (
             TerminatingOrDeleted(core.Comp.RemoteEntity) ||
             !HasComp<AIEyeComponent>(core.Comp.RemoteEntity)
         )
             return;

         if(!TryComp<AIEyeComponent>(core.Comp.RemoteEntity, out var eye))
             return;

         if(eye.Camera == null || TerminatingOrDeleted(eye.Camera))
             return;

         if (_transform.GetGrid(args.Target) != Transform(ent).GridUid)
             return;

         if (!TryComp<SurveillanceCameraComponent>(eye.Camera, out var camera))
             return;

         if (!camera.Active)
         {
             _popup.PopupCursor("камера не работает!", core.Comp.RemoteEntity.Value, PopupType.LargeCaution);
             return;
         }

         args.Handled = true;

         var targetPos = _transform.ToMapCoordinates(args.Target);
         var camPos = Transform(eye.Camera.Value).Coordinates;
         var camMapPos = _transform.ToMapCoordinates(camPos);

         var ammo = Spawn(BulletDisabler, camPos);
         _gun.ShootProjectile(ammo, targetPos.Position - camMapPos.Position, Vector2.One, eye.Camera.Value, args.Performer);
         _audio.PlayPvs("/Audio/Weapons/Guns/Gunshots/taser2.ogg", eye.Camera.Value);
     }

     private void OnRemove(Entity<AICameraComponent> ent, ref EntityTerminatingEvent args)
     {
         OnCameraOffline(ent);
     }

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
         if (ent.Comp.IsProcessingMoveEvent || !args.NewPosition.IsValid(EntityManager))
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
