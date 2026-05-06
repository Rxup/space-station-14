using Content.Shared.Inventory;
using Content.Shared.Storage;
using Content.Shared.Storage.Components;
using Robust.Shared.Containers;

namespace Content.Shared.Backmen.Storage;

/// <summary>
/// Blocks direct interaction with storage equipped by another entity.
/// This forces these interactions to go through stripping flow.
/// </summary>
public sealed class BackmenStorageInteractSystem : EntitySystem
{
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<StorageComponent, StorageInteractAttemptEvent>(OnStorageInteractAttempt);
    }

    private void OnStorageInteractAttempt(Entity<StorageComponent> ent, ref StorageInteractAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        if (!_container.TryGetContainingContainer(ent.Owner, out var container))
            return;

        if (!_inventory.TryGetSlot(container.Owner, container.ID, out _))
            return;

        if (container.Owner != args.User)
            args.Cancelled = true;
    }
}
