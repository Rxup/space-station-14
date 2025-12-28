using Robust.Shared.Serialization;

namespace Content.Shared._Backmen.Silicon;

[Serializable, NetSerializable]
public enum SiliconChargerVisuals
{
    Lights,
}

[Serializable, NetSerializable]
public enum SiliconChargerVisualState
{
    Normal,
    NormalOpen,
    Charging
}
