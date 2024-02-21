using System.Numerics;
using Content.Server.Interaction;
using Content.Server.Lightning;
using Content.Server.Popups;
using Content.Server.SurveillanceCamera;
using Content.Server.Weapons.Ranged.Systems;
using Content.Shared.Backmen.StationAI;
using Content.Shared.Eye.Blinding.Components;
using Content.Shared.Popups;
using Content.Shared.Tag;
using Content.Shared.Traits.Assorted;
using Content.Shared.Weapons.Ranged;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.CPUJob.JobQueues.Queues;
using Robust.Shared.Prototypes;

namespace Content.Server.Backmen.StationAI.Systems;

public sealed class AICameraSystem : EntitySystem
{
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly InteractionSystem _interaction = default!;
    [Dependency] private readonly SurveillanceCameraSystem _cameraSystem = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly GunSystem _gun = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly MapSystem _map = default!;
    [Dependency] private readonly TagSystem _tag = default!;



    private const double MoverJobTime = 0.005;
    private readonly JobQueue _moveJobQueue = new(MoverJobTime);



    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AICameraComponent, ComponentStartup>(HandleCameraStartup);

        SubscribeLocalEvent<AIEyeComponent, MoveEvent>(OnEyeMove);
        SubscribeLocalEvent<AICameraComponent, SurveillanceCameraDeactivateEvent>(OnActiveCameraDisable);
        SubscribeLocalEvent<AICameraComponent, EntityTerminatingEvent>(OnRemove);
        SubscribeLocalEvent<AIEyeComponent, EyeMoveToCam>(OnMoveToCam);
        SubscribeLocalEvent<AIEyeComponent, AIEyeCampShootActionEvent>(OnShootCam);
    }

    private void OnRemove(Entity<AICameraComponent> ent, ref EntityTerminatingEvent args)
    {
        OnCameraOffline(ent);
    }

    [ValidatePrototypeId<EntityPrototype>]
    private const string BulletDisabler = "BulletDisabler";

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

        var job = new AiEyeMover(EntityManager, this, _lookup, _transform, _map, _tag, MoverJobTime)
        {
            Eye = ent,
            //OldPosition = args.OldPosition,
            NewPosition = args.NewPosition
        };

        _moveJobQueue.EnqueueJob(job);
    }

    public bool IsCameraActive(Entity<AIEyeComponent> eye)
    {
        if (!eye.Comp.Camera.HasValue)
            return false;

        return _interaction.InRangeUnobstructed(eye.Comp.Camera.Value, eye, SharedStationAISystem.CameraEyeRange);
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

    public void HandleMove(Entity<AIEyeComponent> eye, HashSet<Entity<SurveillanceCameraComponent>> cameraComponents)
    {
        foreach (var cameraComponent in cameraComponents)
        {
            var v = cameraComponent.Comp.ActiveViewers;
            if(!cameraComponent.Comp.Active)
                continue;

            if(!_interaction.InRangeUnobstructed(cameraComponent, eye, SharedStationAISystem.CameraEyeRange))
                continue;

            RemCompDeferred<TemporaryBlindnessComponent>(eye);
            ChangeActiveCamera(eye, cameraComponent, cameraComponent.Comp);
            return;
        }

        if (eye.Comp.Camera.HasValue)
        {
            RemoveActiveCamera(eye);
        }

        if(eye.Owner is { Valid: true })
            EnsureComp<TemporaryBlindnessComponent>(eye);
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
