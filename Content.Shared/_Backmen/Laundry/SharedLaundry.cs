using Robust.Shared.Serialization;

namespace Content.Shared._Backmen.Laundry;

[RegisterComponent]
public sealed partial class SharedWashingMachineComponent : Component
{

}

[Serializable, NetSerializable]
public enum WashingMachineVisualState : byte
{
    Broken,
}
