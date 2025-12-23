using Content.Shared._Backmen.Shipwrecked.Components;
using Content.Shared.Storage.Components;

namespace Content.Shared._Backmen.Shipwrecked;

public sealed class HologramSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HologramComponent, InsertIntoEntityStorageAttemptEvent>(OnInsertInStorage);
    }

    private void OnInsertInStorage(EntityUid uid, HologramComponent component, ref InsertIntoEntityStorageAttemptEvent args)
    {
        // Forbid holograms from going inside anything.
        args.Cancelled = true;
    }
}
