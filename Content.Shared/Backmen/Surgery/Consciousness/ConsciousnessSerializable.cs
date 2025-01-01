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
public sealed class ConsciousnessComponentState : ComponentState
{
    public FixedPoint2 Threshold;
    public FixedPoint2 RawConsciousness;
    public FixedPoint2 Multiplier;
    public FixedPoint2 Cap;

    public readonly Dictionary<(NetEntity, ConsciousnessModType), ConsciousnessModifier> Modifiers = new();
    public readonly Dictionary<(NetEntity, ConsciousnessModType), ConsciousnessMultiplier> Multipliers = new();
    public readonly Dictionary<string, (NetEntity?, bool, bool)> RequiredConsciousnessParts = new();

    public bool ForceDead;
    public bool ForceUnconscious;
    public bool IsConscious;
}

[ByRefEvent]
public record struct ConsciousnessUpdatedEvent(bool IsConscious, FixedPoint2 ConsciousnessDelta);

[Serializable, DataRecord]
public record struct ConsciousnessModifier(FixedPoint2 Change, string Identifier = "Unspecified");

[Serializable, DataRecord]
public record struct ConsciousnessMultiplier(FixedPoint2 Change, string Identifier = "Unspecified", ConsciousnessModType Type = ConsciousnessModType.Generic);
