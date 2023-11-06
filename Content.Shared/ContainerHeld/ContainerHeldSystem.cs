using Robust.Shared.Containers;

using Content.Shared.Item;
using Content.Shared.Storage;
using Content.Shared.Storage.EntitySystems;
using Content.Shared.Toggleable;

namespace Content.Shared.ContainerHeld;

public sealed class ContainerHeldSystem : EntitySystem
{
    [Dependency] private readonly SharedItemSystem _item = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedStorageSystem _storage = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ContainerHeldComponent, EntInsertedIntoContainerMessage>(OnContainerModified);
        SubscribeLocalEvent<ContainerHeldComponent, EntRemovedFromContainerMessage>(OnContainerModified);
    }

    private void OnContainerModified(EntityUid uid, ContainerHeldComponent comp, ContainerModifiedMessage args)
    {
        if (!(TryComp<StorageComponent>(uid, out var storage)
              && TryComp<AppearanceComponent>(uid, out var appearance)
              && TryComp<ItemComponent>(uid, out var item)))
        {
            return;
        }
        if (_storage.GetCumulativeItemSizes(uid, storage) >= comp.Threshold)
        {
            _item.SetHeldPrefix(uid, "full", item);
            _appearance.SetData(uid, ToggleVisuals.Toggled, true, appearance);
        }
        else
        {
            _item.SetHeldPrefix(uid, "empty", item);
            _appearance.SetData(uid, ToggleVisuals.Toggled, false, appearance);
        }
    }
}
