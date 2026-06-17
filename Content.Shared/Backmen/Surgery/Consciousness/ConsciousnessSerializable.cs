using Content.Shared.Backmen.Surgery.Consciousness.Components;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Robust.Shared.Serialization;

namespace Content.Shared.Backmen.Surgery.Consciousness;

[Serializable]
public enum ConsciousnessModType
{
    Generic, // Same for generic
    Pain, // Pain is affected only by pain multipliers
}

[Serializable, NetSerializable]
public sealed partial class CprDoAfterEvent : SimpleDoAfterEvent;

[ByRefEvent]
public record struct ConsciousUpdateEvent(ConsciousnessComponent Consciousness, bool IsConscious);

[ByRefEvent]
public record struct ConsciousnessChangedEvent(
    ConsciousnessComponent Component,
    FixedPoint2 NewConsciousness,
    FixedPoint2 OldConsciousness);

[Serializable, DataRecord]
public partial record struct ConsciousnessModifier(FixedPoint2 Change, TimeSpan? Time, ConsciousnessModType Type = ConsciousnessModType.Generic);

[Serializable, DataRecord]
public partial record struct ConsciousnessMultiplier(FixedPoint2 Change, TimeSpan? Time, ConsciousnessModType Type = ConsciousnessModType.Generic);
