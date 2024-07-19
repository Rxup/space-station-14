using Content.Shared.Backmen.CameraFollow.Components;
using Content.Shared.Backmen.FollowDistance.Components;
using Content.Shared.Hands;

namespace Content.Shared.Backmen.FollowDistance;
/// <summary>
/// System to set new max distance and back strength for <see cref="CameraFollowComponent"/>
/// </summary>
public sealed class FollowDistanceSystem : EntitySystem
{
    public override void Initialize()
    {
        SubscribeLocalEvent<FollowDistanceComponent, HandSelectedEvent>(OnPickedUp);
        SubscribeLocalEvent<FollowDistanceComponent, HandDeselectedEvent>(OnDropped);
    }

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
