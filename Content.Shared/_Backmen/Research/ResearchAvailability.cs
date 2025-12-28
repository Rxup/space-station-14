using Robust.Shared.Serialization;

namespace Content.Shared._Backmen.Research;

[Serializable, NetSerializable]
public enum ResearchAvailability : byte
{
    Researched,
    Available,
    PrereqsMet,
    Unavailable
}
