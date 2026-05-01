using Robust.Shared.Serialization;

namespace Content.Shared.Backmen.Body;

[Obsolete]
[Serializable, NetSerializable]
public enum BodyPartType : byte
{
    Other = 0,
    Chest,
    Groin,
    Head,
    Arm,
    Hand,
    Leg,
    Foot,
    Tail,
}

/// <summary>
///     Defines the symmetry of a <see cref="BodyComponent"/>.
/// </summary>
[Obsolete]
[Serializable, NetSerializable]
public enum BodyPartSymmetry
{
    None = 0,
    Left,
    Right
}
