using Content.Shared.Storage.Components;

namespace Content.Server.Backmen.Shipwrecked.Holograms;

public sealed class HologramSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HologramComponent, StoreMobInItemContainerAttemptEvent>(OnStoreInContainerAttempt);
        SubscribeLocalEvent<HologramComponent, InsertIntoEntityStorageAttemptEvent>(OnInsertInStorage);
    }

    private void OnInsertInStorage(EntityUid uid, HologramComponent component, ref InsertIntoEntityStorageAttemptEvent args)
    {
        args.Cancelled = true;
    }

    private void OnStoreInContainerAttempt(EntityUid uid, HologramComponent component, ref StoreMobInItemContainerAttemptEvent args)
    {
        // TODO: It should be okay to move this to Shared.
        // Forbid holograms from going inside anything.
        args.Cancelled = true;
        args.Handled = true;
    }
}
