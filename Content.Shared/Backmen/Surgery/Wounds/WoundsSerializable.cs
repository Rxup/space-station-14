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

/// <summary>
/// Wounds are sorted by added, removed and changed.
///
/// Added wounds pass a wound entity, the wound severity point and other values are not changed.
/// Removed wounds pass a wound entity, the wound severity point is passed before the delition, after the event the entity is deleted.
/// Changed wounds pass a wound entity and the wound delta.
/// </summary>
[ByRefEvent]
public record struct WoundsChangedEvent(
    EntityUid? Origin,
    List<Entity<WoundComponent>> AddedWounds,
    List<Entity<WoundComponent>> RemovedWounds,
    Dictionary<Entity<WoundComponent>, FixedPoint2> ChangedWounds,
    bool DamageIncreased = false);

/// <summary>
/// This one is just, wound added, changed and removed mashed into an event. passes a delta,
///
/// If added - the starting severity,
/// If changed - the actual delta,
/// If removed - the severity before removal, minused. (eg 7 severity wound healed would be -7 delta)
/// </summary>
[ByRefEvent]
public record struct WoundChangedEvent(WoundComponent Component, FixedPoint2 Delta);

/// <summary>
/// Wounds are not sorted unlike WoundsChangedEvent, where you can differentiate added/removed wounds from already present, changed wounds.
/// Use this instead of the Changed events if you don't need to specifically know ADDED nor REMOVED wounds
///
/// Total Delta is explanatory
///
/// Added wounds have their wound severity passed as value,
/// Removed wounds pass their severity, minused. e.g., 12 severity wound in this list will pass -12.
/// Changed wounds pass the delta.
/// </summary>
[ByRefEvent]
public record struct WoundsDeltaChanged(
    EntityUid? Origin,
    FixedPoint2 TotalDelta,
    Dictionary<Entity<WoundComponent>, FixedPoint2> WoundsDelta,
    bool DamageIncreased = false);

/// <summary>
/// lets you know alllll the unhandled consciousness wounds' damage and stuff for whatever implementation you want.
/// </summary>
[ByRefEvent]
public record struct HandleUnhandledWoundsEvent(Dictionary<string, FixedPoint2> UnhandledDamage);

[ByRefEvent]
public record struct WoundAddedEvent(WoundComponent Component, WoundableComponent Woundable, WoundableComponent RootWoundable);

[ByRefEvent]
public record struct WoundRemovedEvent(WoundComponent Component, WoundableComponent OldWoundable, WoundableComponent OldRootWoundable);

[ByRefEvent]
public record struct WoundableAttachedEvent(EntityUid ParentWoundableEntity, WoundableComponent Component);

[ByRefEvent]
public record struct WoundableDetachedEvent(EntityUid ParentWoundableEntity, WoundableComponent Component);

[ByRefEvent]
public record struct WoundSeverityPointChangedEvent(WoundComponent Component, FixedPoint2 OldSeverity, FixedPoint2 NewSeverity);

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
