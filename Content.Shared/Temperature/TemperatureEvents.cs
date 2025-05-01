using Content.Shared.Backmen.Targeting;
using Content.Shared.Inventory;

namespace Content.Shared.Temperature;

public sealed class ModifyChangedTemperatureEvent : EntityEventArgs, IInventoryRelayEvent
{
    public SlotFlags TargetSlots { get; } = ~SlotFlags.POCKET;

    public float TemperatureDelta;
    public readonly TargetBodyPart? TargetBodyPart; // backmen change

    public ModifyChangedTemperatureEvent(float temperature, TargetBodyPart? targetBodyPart = null) // backmen change
    {
        TemperatureDelta = temperature;
        TargetBodyPart = targetBodyPart; // backmen change
    }
}

public sealed class OnTemperatureChangeEvent : EntityEventArgs
{
    public readonly float CurrentTemperature;
    public readonly float LastTemperature;
    public readonly float TemperatureDelta;

    public OnTemperatureChangeEvent(float current, float last, float delta)
    {
        CurrentTemperature = current;
        LastTemperature = last;
        TemperatureDelta = delta;
    }
}

