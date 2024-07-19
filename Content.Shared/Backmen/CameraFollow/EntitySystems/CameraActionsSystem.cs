using System.Numerics;
using Content.Shared.Backmen.CameraFollow.Components;
using Content.Shared.Backmen.CameraFollow.Events;
using Content.Shared.Bed.Sleep;

namespace Content.Shared.Backmen.CameraFollow.EntitySystems;

public sealed class CameraActionsSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CameraFollowComponent, ToggleCameraEvent>(OnToggleCamera);
    }

    private void OnToggleCamera(EntityUid uid, CameraFollowComponent component, ToggleCameraEvent args)
    {
        if (HasComp<SleepingComponent>(uid)) // Check if entity is sleeping right now(when sleeping entity has a shader without shadows, it can cause wallhacking)
        {
            args.Handled = true;
            return;
        }

        SetCameraEnabled(component, !component.Enabled);
        Dirty(uid, component);
        args.Handled = true;
    }


    /// <summary>
    /// Sets the enabled state of the camera on server and client side
    /// TODO: I think its strange func. and needs to be refactored? Probably had to be moved to Content.Server.Backmen;
    /// </summary>
    /// <param name="component">CameraFollowComponent</param>
    /// <param name="enabled">Enabled boolean value</param>
    private void SetCameraEnabled(CameraFollowComponent component, bool enabled)
    {
        component.Enabled = enabled;
        component.Offset = new Vector2(0, 0);
    }
}
