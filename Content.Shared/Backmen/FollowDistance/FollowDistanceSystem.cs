using Content.Shared.Backmen.CameraFollow.Components;
using Content.Shared.Backmen.FollowDistance.Components;
using Content.Shared.Camera;
using Content.Shared.Hands;

namespace Content.Shared.Backmen.FollowDistance;
/// <summary>
/// System to set new max distance and back strength for <see cref="CameraFollowComponent"/>
/// </summary>
public sealed class FollowDistanceSystem : EntitySystem
{
    [Dependency] private readonly Actions.SharedActionsSystem _actionsSystem = default!; // Stalker-Changes
    private EntityQuery<CameraRecoilComponent> _activeRecoil;

    public override void Initialize()
    {
        SubscribeLocalEvent<FollowDistanceComponent, HandSelectedEvent>(OnPickedUp);
        SubscribeLocalEvent<FollowDistanceComponent, HandDeselectedEvent>(OnDropped);
        SubscribeLocalEvent<CameraFollowComponent, ComponentRemove>(OnCameraFollowRemove);
        SubscribeLocalEvent<CameraFollowComponent, ComponentInit>(OnCameraFollowInit);

        SubscribeLocalEvent<CameraFollowComponent, GetEyeOffsetEvent>(OnCameraRecoilGetEyeOffset);

        _activeRecoil = GetEntityQuery<CameraRecoilComponent>();
    }

    private void OnCameraRecoilGetEyeOffset(Entity<CameraFollowComponent> ent, ref GetEyeOffsetEvent arg)
    {
        if (!_activeRecoil.TryComp(ent, out var recoil))
            return;

        arg.Offset = recoil.BaseOffset + recoil.CurrentKick + ent.Comp.Offset; // Stalker-Changes
    }

    private void OnCameraFollowInit(EntityUid uid, CameraFollowComponent component, ComponentInit args) // Stalker-Changes-Start
    {
        _actionsSystem.AddAction(uid, ref component.ActionEntity, component.Action);
    }

    private void OnCameraFollowRemove(EntityUid uid, CameraFollowComponent component, ComponentRemove args)
    {
        _actionsSystem.RemoveAction(uid, component.ActionEntity);
    } // Stalker-Changes-End

    private void OnPickedUp(EntityUid uid, FollowDistanceComponent followDistance, HandSelectedEvent args)
    {
        if (!TryComp<CameraFollowComponent>(args.User, out var camfollow) && !HasComp<EyeComponent>(args.User))
            return;
        if (camfollow == null || !camfollow.Enabled)
            return;

        camfollow.MaxDistance = followDistance.MaxDistance;
        camfollow.BackStrength = followDistance.BackStrength;
        Dirty(args.User, camfollow);
    }

    private void OnDropped(EntityUid uid, FollowDistanceComponent followDistance, HandDeselectedEvent args)
    {
        if (!TryComp<CameraFollowComponent>(args.User, out var camfollow) && !HasComp<EyeComponent>(args.User))
            return;
        if (camfollow == null)
            return;

        camfollow.MaxDistance = camfollow.DefaultMaxDistance;
        camfollow.BackStrength = camfollow.DefaultBackStrength;
        Dirty(args.User, camfollow);
    }

}
