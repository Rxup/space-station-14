using Content.Server.Interaction;
using Content.Server.SurveillanceCamera;
using Content.Shared.Backmen.StationAI;
using Content.Shared.Eye.Blinding.Components;
using Content.Shared.Traits.Assorted;
using Robust.Server.GameObjects;
using Robust.Shared.CPUJob.JobQueues.Queues;

namespace Content.Server.Backmen.StationAI.Systems;

public sealed class AICameraSystem : EntitySystem
{
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly InteractionSystem _interaction = default!;
    [Dependency] private readonly SurveillanceCameraSystem _cameraSystem = default!;


    public const float CameraEyeRange = 10f;
    private const double MoverJobTime = 0.005;
    private readonly JobQueue _moveJobQueue = new(MoverJobTime);

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AICameraComponent, ComponentStartup>(HandleCameraStartup);

        SubscribeLocalEvent<AIEyeComponent, MoveEvent>(OnEyeMove);
        SubscribeLocalEvent<AICameraComponent, SurveillanceCameraDeactivateEvent>(OnActiveCameraDisable);
    }

    private void OnActiveCameraDisable(Entity<AICameraComponent> ent, ref SurveillanceCameraDeactivateEvent args)
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

        var job = new AiEyeMover(EntityManager, this, _lookup, _transform, MoverJobTime)
        {
            Eye = ent,
            NewPosition = args.NewPosition
        };

        _moveJobQueue.EnqueueJob(job);
    }

    public bool IsCameraActive(Entity<AIEyeComponent> eye)
    {
        if (!eye.Comp.Camera.HasValue)
            return false;

        return _interaction.InRangeUnobstructed(eye.Comp.Camera.Value, eye, CameraEyeRange);
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

            if(!_interaction.InRangeUnobstructed(cameraComponent, eye, CameraEyeRange))
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
