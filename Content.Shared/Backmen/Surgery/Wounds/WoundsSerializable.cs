using Content.Shared.Backmen.Surgery.Wounds.Components;
using Content.Shared.FixedPoint;
using Robust.Shared.Serialization;

namespace Content.Shared.Backmen.Surgery.Wounds;

[Serializable, NetSerializable]
public enum WoundType
{
    External,
    Internal,
}

[Serializable, NetSerializable]
public enum WoundSeverity
{
    Healed,
    Minor,
    Moderate,
    Severe,
    Critical,
    Loss,
}

[Serializable, NetSerializable]
public enum BleedingSeverity
{
    Minor,
    Severe,
}

[Serializable, NetSerializable]
public enum WoundableSeverity : byte
{
    Healthy,
    Minor,
    Moderate,
    Severe,
    Critical,
    Loss,
}

[Serializable, NetSerializable]
public enum WoundVisibility
{
    Always,
    HandScanner,
    AdvancedScanner,
}

[ByRefEvent]
public record struct WoundAddedEvent(WoundComponent Component, WoundableComponent Woundable, WoundableComponent RootWoundable);

[ByRefEvent]
public record struct WoundAddedOnBodyEvent(Entity<WoundComponent> Wound, WoundableComponent Woundable, WoundableComponent RootWoundable);

[ByRefEvent]
public record struct WoundRemovedEvent(WoundComponent Component, WoundableComponent OldWoundable, WoundableComponent OldRootWoundable);

[ByRefEvent]
public record struct WoundableAttachedEvent(EntityUid ParentWoundableEntity, WoundableComponent Component);

[ByRefEvent]
public record struct WoundableDetachedEvent(EntityUid ParentWoundableEntity, WoundableComponent Component);

[ByRefEvent]
public record struct WoundSeverityPointChangedEvent(WoundComponent Component, FixedPoint2 OldSeverity, FixedPoint2 NewSeverity);

[ByRefEvent]
public record struct WoundSeverityPointChangedOnBodyEvent(Entity<WoundComponent> Wound, FixedPoint2 OldSeverity, FixedPoint2 NewSeverity);

[ByRefEvent]
public record struct WoundSeverityChangedEvent(WoundSeverity OldSeverity, WoundSeverity NewSeverity);

[ByRefEvent]
public record struct WoundableIntegrityChangedEvent(FixedPoint2 OldIntegrity, FixedPoint2 NewIntegrity);

[ByRefEvent]
public record struct WoundableIntegrityChangedOnBodyEvent(Entity<WoundableComponent> Woundable, FixedPoint2 OldIntegrity, FixedPoint2 NewIntegrity);

[ByRefEvent]
public record struct WoundableSeverityChangedEvent(WoundableSeverity OldSeverity, WoundableSeverity NewSeverity);

[ByRefEvent]
public record struct WoundHealAttemptEvent(Entity<WoundableComponent> Woundable, bool Cancelled = false);

[ByRefEvent]
public record struct WoundHealAttemptOnWoundableEvent(Entity<WoundComponent> Wound, bool Cancelled = false);

[Serializable, DataRecord]
public record struct WoundableSeverityMultiplier(FixedPoint2 Change, string Identifier = "Unspecified");

[Serializable, DataRecord]
public record struct WoundableHealingMultiplier(FixedPoint2 Change, string Identifier = "Unspecified");
