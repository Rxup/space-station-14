namespace Content.Shared.Backmen.Abilities.Psionics;

/// <summary>
/// Raised when <see cref="PsionicInsulationComponent"/> is applied/removed via a status effect.
/// Broadcast so other systems can react without a second (Component, StatusEffect*) subscription.
/// </summary>
[ByRefEvent]
public readonly record struct PsionicInsulationStatusChangedEvent(EntityUid Target, EntityUid Effect, bool Applied);
