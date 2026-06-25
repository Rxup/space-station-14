namespace Content.Shared.Backmen.Body.OrganRelations;

/// <summary>
/// Raised when a detached bundle's root organ is heat-destroyed and should burn in stages.
/// </summary>
[ByRefEvent]
public readonly record struct BurnDetachedBundleRootRequestEvent;
