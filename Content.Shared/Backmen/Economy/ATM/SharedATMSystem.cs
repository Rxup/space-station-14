using Content.Shared.Containers.ItemSlots;

namespace Content.Shared.Backmen.Economy.ATM;

public abstract class SharedATMSystem : EntitySystem
{
    [Dependency] private readonly ItemSlotsSystem _itemSlotsSystem = default!;
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SharedAtmComponent, ComponentInit>(OnComponentInit);
        SubscribeLocalEvent<SharedAtmComponent, ComponentRemove>(OnComponentRemove);
    }
    private void OnComponentInit(EntityUid uid, SharedAtmComponent component, ComponentInit args)
    {
        _itemSlotsSystem.AddItemSlot(uid, SharedAtmComponent.IdCardSlotId, component.IdCardSlot);
    }

    private void OnComponentRemove(EntityUid uid, SharedAtmComponent component, ComponentRemove args)
    {
        _itemSlotsSystem.RemoveItemSlot(uid, component.IdCardSlot);
    }
}
