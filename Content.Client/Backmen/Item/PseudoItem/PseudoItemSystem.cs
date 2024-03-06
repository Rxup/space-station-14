using Content.Client.Popups;
using Content.Shared.Backmen.Item.PseudoItem;
using Content.Shared.Popups;
using Content.Shared.Storage;

namespace Content.Client.Backmen.Item.PseudoItem;

public sealed class PseudoItemSystem : SharedPseudoItemSystem
{
    [Dependency] private PopupSystem _popup = default!;

    public override bool TryInsert(EntityUid storageUid, Entity<PseudoItemComponent> toInsert, EntityUid? user, StorageComponent? storage = null)
    {
        if (!Resolve(storageUid, ref storage))
            return false;

        if (!CanInsertInto(toInsert, storageUid, storage))
        {
            if (user.HasValue)
            {
                _popup.PopupEntity(Loc.GetString("comp-storage-too-big"), toInsert, user.Value, PopupType.LargeCaution);
            }
            return false;
        }

        return true;
    }
}
