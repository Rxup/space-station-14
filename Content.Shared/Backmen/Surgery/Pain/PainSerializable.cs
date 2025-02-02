using Content.Shared.Backmen.Surgery.Pain.Components;
using Content.Shared.FixedPoint;
using Robust.Shared.Serialization;

namespace Content.Shared.Backmen.Surgery.Pain;

[Serializable, NetSerializable]
public enum PainThresholdTypes
{
    None,
    PainFlinch,
    PainShock,
    PainPassout,
}

[Serializable, DataRecord]
public record struct PainMultiplier(FixedPoint2 Change, string Identifier = "Unspecified");

[Serializable, DataRecord]
public record struct PainModifier(FixedPoint2 Change, string Identifier = "Unspecified"); // Easier to manage pain with modifiers.

[ByRefEvent]
public record struct PainThresholdTriggered(EntityUid NerveSystem, NerveSystemComponent Component, PainThresholdTypes ThresholdType, FixedPoint2 PainInput, bool Cancelled = false);

[ByRefEvent]
public record struct PainThresholdEffected(EntityUid NerveSystem, NerveSystemComponent Component, PainThresholdTypes ThresholdType, FixedPoint2 PainInput);

[ByRefEvent]
public record struct PainModifierAddedEvent(EntityUid NerveSystem, EntityUid NerveUid, FixedPoint2 AddedPain);

[ByRefEvent]
public record struct PainModifierRemovedEvent(EntityUid NerveSystem, EntityUid NerveUid, FixedPoint2 CurrentPain);

[ByRefEvent]
public record struct PainModifierChangedEvent(EntityUid NerveSystem, EntityUid NerveUid, FixedPoint2 CurrentPain);
