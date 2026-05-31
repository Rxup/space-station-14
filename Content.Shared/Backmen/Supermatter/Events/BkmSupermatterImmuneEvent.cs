using Content.Shared.Inventory;

namespace Content.Shared.Backmen.Supermatter.Events;

public sealed class BkmSupermatterImmuneEvent(EntityUid target, EntityUid source) : CancellableEntityEventArgs, IInventoryRelayEvent
{
    public EntityUid Target { get; } = target;
    public EntityUid Source { get; } = source;

    public SlotFlags TargetSlots { get; } = ~SlotFlags.POCKET;
}
