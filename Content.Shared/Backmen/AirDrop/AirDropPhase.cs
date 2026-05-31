using Robust.Shared.Serialization;

namespace Content.Shared.Backmen.AirDrop;

[Serializable, NetSerializable]
public enum AirDropPhase : byte
{
    Inactive = 0,
    Target = 1,
    Drop = 2,
    Done = 3,
}
