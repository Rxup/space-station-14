using Content.Shared.Backmen.Blob.Components;
using Content.Shared.Popups;
using Content.Shared.Weapons.Ranged.Events;

namespace Content.Shared.Backmen.Blob;

public abstract class SharedZombieBlobSystem : EntitySystem
{
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ZombieBlobComponent, ShotAttemptedEvent>(OnAttemptShoot);
    }

    private void OnAttemptShoot(Entity<ZombieBlobComponent> ent, ref ShotAttemptedEvent args)
    {
        if(ent.Comp.CanShoot)
            return;

        _popup.PopupClient(Loc.GetString("blob-no-using-guns-popup"), ent, ent);
        args.Cancel();
    }
}
