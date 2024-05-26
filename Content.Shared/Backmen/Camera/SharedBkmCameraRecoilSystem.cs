using Content.Shared.Backmen.Camera.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Wieldable.Components;

namespace Content.Shared.Backmen.Camera;

public sealed class SharedBkmCameraRecoilSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BkmCameraRecoilComponent, GunRefreshModifiersEvent>(OnGunRefreshModifiers);
    }

    private void OnGunRefreshModifiers(EntityUid uid, BkmCameraRecoilComponent component, ref GunRefreshModifiersEvent args)
    {
        if (TryComp(uid, out WieldableComponent? wield) &&
            wield.Wielded)
        {
            args.CameraRecoilScalar += component.CameraRecoilScalar;
        }
    }
}
