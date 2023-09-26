using Robust.Shared.Serialization;

namespace Content.Shared.Backmen.Laundry;

[RegisterComponent]
public sealed partial class SharedWashingMachineComponent : Component
{

}

[Serializable, NetSerializable]
public enum WashingMachineVisualState : byte
{
    Broken,
}
