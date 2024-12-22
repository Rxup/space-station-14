using Content.Shared.FixedPoint;

namespace Content.Shared.Backmen.Surgery.Pain;

[Serializable, DataRecord]
public record struct PainMultiplier(FixedPoint2 Change, string Identifier = "Unspecified");

[Serializable, DataRecord]
public record struct PainModifier(FixedPoint2 Change, string Identifier = "Unspecified"); // Easier to manage pain with modifiers.

[ByRefEvent]
public record struct PainModifierAddedEvent(EntityUid NerveSystem, EntityUid NerveUid, FixedPoint2 AddedPain);

[ByRefEvent]
public record struct PainModifierRemovedEvent(EntityUid NerveSystem, EntityUid NerveUid, FixedPoint2 CurrentPain);

[ByRefEvent]
public record struct PainModifierChangedEvent(EntityUid NerveSystem, EntityUid NerveUid, FixedPoint2 CurrentPain);
