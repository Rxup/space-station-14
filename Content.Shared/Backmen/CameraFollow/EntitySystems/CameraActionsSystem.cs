using System.Numerics;
using Content.Shared.Backmen.CameraFollow.Components;
using Content.Shared.Backmen.CameraFollow.Events;
using Content.Shared.Bed.Sleep;
using Content.Shared.Stunnable;

namespace Content.Shared.Backmen.CameraFollow.EntitySystems;

public sealed class CameraActionsSystem : EntitySystem
{
    [Dependency] private readonly SharedEyeSystem _eye = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CameraFollowComponent, ToggleCameraEvent>(OnToggleCamera);
        SubscribeLocalEvent<CameraFollowComponent, SleepStateChangedEvent>(OnSleeping);
    }

    private void OnSleeping(Entity<CameraFollowComponent> ent, ref SleepStateChangedEvent args)
    {
        SetCameraEnabled(ent, false);
        Dirty(ent);
    }

    private void OnToggleCamera(EntityUid uid, CameraFollowComponent component, ToggleCameraEvent args)
    {
        if (HasComp<SleepingComponent>(uid) || HasComp<StunnedComponent>(uid)) // Check if entity is sleeping right now(when sleeping entity has a shader without shadows, it can cause wallhacking)
        {
            args.Handled = true;
            return;
        }

        SetCameraEnabled((uid,component), !component.Enabled);
        Dirty(uid, component);
        args.Handled = true;
    }


    /// <summary>
    /// Sets the enabled state of the camera on server and client side
    /// TODO: I think its strange func. and needs to be refactored? Probably had to be moved to Content.Server.Backmen;
    /// </summary>
    /// <param name="component">CameraFollowComponent</param>
    /// <param name="enabled">Enabled boolean value</param>
    private void SetCameraEnabled(Entity<CameraFollowComponent> component, bool enabled)
    {
        component.Comp.Enabled = enabled;
        component.Comp.Offset = new Vector2(0, 0);
        _eye.SetOffset(component, component.Comp.Offset);
    }
}
