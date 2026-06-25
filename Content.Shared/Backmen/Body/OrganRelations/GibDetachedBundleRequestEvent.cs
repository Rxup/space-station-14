namespace Content.Shared.Backmen.Body.OrganRelations;

/// <summary>
/// Raised when a detached bundle's root organ is destroyed and contained organs should be ejected.
/// </summary>
[ByRefEvent]
public readonly record struct GibDetachedBundleRequestEvent;
