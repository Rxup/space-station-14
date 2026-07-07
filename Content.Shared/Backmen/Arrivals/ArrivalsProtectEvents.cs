using Content.Shared.Wires;

namespace Content.Shared.Backmen.Arrivals;

/// <summary>
/// Raised before a wire action is performed on an entity protected by arrivals.
/// </summary>
[ByRefEvent]
public record struct WiresActionAttemptEvent(EntityUid User, WiresAction Action, bool Cancelled = false);
