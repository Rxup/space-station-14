using Content.Shared.FixedPoint;
using Robust.Shared.Serialization;

namespace Content.Shared.Backmen.Surgery.Consciousness;

[Serializable]
public enum ConsciousnessModType
{
    Generic, // Same for generic
    Pain, // Pain is affected only by pain multipliers
}

[ByRefEvent]
public record struct ConsciousnessUpdatedEvent(bool IsConscious, FixedPoint2 ConsciousnessDelta);

[Serializable, DataRecord]
public record struct ConsciousnessModifier(FixedPoint2 Change, string Identifier = "Unspecified");

[Serializable, DataRecord]
public record struct ConsciousnessMultiplier(FixedPoint2 Change, string Identifier = "Unspecified", ConsciousnessModType Type = ConsciousnessModType.Generic);
