using Robust.Shared.Serialization;

namespace Content.Shared.Backmen.BluespaceMining;

[Serializable, NetSerializable]
public enum BluespaceMinerVisuals : byte
{
    State
}

[Serializable, NetSerializable]
public enum BluespaceMinerVisualState : byte
{
    IsOff,
    IsRunning
}
