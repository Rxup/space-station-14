using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;

namespace Content.Shared.Backmen.Inventory;

/// <summary>
/// Handles prevention of items being unequipped and equipped from slots that are blocked by <see cref="CoveredSlotComponent"/>.
/// </summary>
public sealed class CoveredSlotSystem : EntitySystem
{
    [Dependency] private readonly InventorySystem _inventory = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<InventoryComponent, IsEquippingAttemptEvent>(OnEquipAttempt);
        SubscribeLocalEvent<InventoryComponent, IsUnequippingAttemptEvent>(OnUnequipAttempt);
    }

    private void OnEquipAttempt(Entity<InventoryComponent> ent, ref IsEquippingAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        var blocker = GetBlocker(ent, args.SlotFlags);

        // Don't do anything if nothing is blocking the entity from equipping.
        if (blocker == null)
            return;

        args.Reason = Loc.GetString("covered-slot-component-blocked", ("item", blocker));
        args.Cancel();
    }

    private void OnUnequipAttempt(Entity<InventoryComponent> ent, ref IsUnequippingAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        var blocker = GetBlocker(ent, args.SlotFlags);

        // Don't do anything if nothing is blocking the entity from unequipping.
        if (blocker == null)
            return;

        args.Reason = Loc.GetString("covered-slot-component-blocked", ("item", blocker));
        args.Cancel();
    }

    /// <summary>
    /// Used to get an entity that is blocking item from being equipped or unequipped.
    /// </summary>
    private EntityUid? GetBlocker(Entity<InventoryComponent> ent, SlotFlags slot)
    {
        foreach (var slotDef in ent.Comp.Slots)
        {
            if (!_inventory.TryGetSlotEntity(ent, slotDef.Name, out var entity))
                continue;

            if (!TryComp<CoveredSlotComponent>(entity, out var blockComponent) || (slot & blockComponent.Slots) == 0)
                continue;

            return entity;
        }

        return null;
    }
}
