using Content.Shared.Backmen.Blob.Components;
using Content.Shared.Movement.Events;

namespace Content.Shared.Backmen.Blob;

public abstract class SharedBlobObserverSystem: EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BlobObserverComponent, UpdateCanMoveEvent>(OnUpdateCanMove);
    }

    private void OnUpdateCanMove(EntityUid uid, BlobObserverComponent component, UpdateCanMoveEvent args)
    {
        if (component.CanMove)
            return;

        args.Cancel();
    }
}
