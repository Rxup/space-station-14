namespace Content.Shared.Backmen.Body;

/// <summary>
/// Raised on a space animal organ after a successful gib harvest roll.
/// </summary>
[ByRefEvent]
public readonly record struct OrganHarvestDamageEvent(float Fraction);
