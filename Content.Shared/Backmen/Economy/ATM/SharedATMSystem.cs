using Content.Shared.Containers.ItemSlots;

namespace Content.Shared.Backmen.Economy.ATM;

public abstract class SharedATMSystem : EntitySystem
{
    [Dependency] private readonly ItemSlotsSystem _itemSlotsSystem = default!;
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SharedATMComponent, ComponentInit>(OnComponentInit);
        SubscribeLocalEvent<SharedATMComponent, ComponentRemove>(OnComponentRemove);
    }
    private void OnComponentInit(EntityUid uid, SharedATMComponent component, ComponentInit args)
    {
        _itemSlotsSystem.AddItemSlot(uid, SharedATMComponent.IdCardSlotId, component.IdCardSlot);
    }

    private void OnComponentRemove(EntityUid uid, SharedATMComponent component, ComponentRemove args)
    {
        _itemSlotsSystem.RemoveItemSlot(uid, component.IdCardSlot);
    }
}
