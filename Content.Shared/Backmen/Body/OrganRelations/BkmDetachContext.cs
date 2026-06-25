namespace Content.Shared.Backmen.Body.OrganRelations;

/// <summary>
/// How a detachable organ left its host body.
/// </summary>
public enum BkmDetachContext
{
    /// <summary>Surgical amputation — neat placement, amputation sound.</summary>
    Surgery,

    /// <summary>Gib, trauma, or wound destroy — wide scatter.</summary>
    Violent,
}
