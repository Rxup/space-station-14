using Content.Shared.Inventory;

namespace Content.Server.Backmen.Species.Shadowkin;

public sealed class RefreshShadowkinPowerModifiersEvent : EntityEventArgs, IInventoryRelayEvent
{
    public SlotFlags TargetSlots { get; } = ~SlotFlags.POCKET;

    public float Modifier { get; private set; } = 1.0f;

    public void ModifySpeed(float multiplier)
    {
        Modifier *= multiplier;
    }
}
