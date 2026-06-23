using Robust.Shared.Serialization;

namespace Content.Shared.Body.Part;

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
