using Robust.Shared.GameObjects;

namespace Content.Server.Backmen.GibOnCollide;

/// <summary>
///  Raise when entity gibbed from GibOnCollideSystem.
/// </summary>
public sealed class GibOnCollideAttemptEvent : EntityEventArgs
{
    public EntityUid GibbedEntity { get; }
    public EntityUid GibberEntity { get; }

    public GibOnCollideAttemptEvent(EntityUid gibbedEntity, EntityUid gibberEntity)
    {
        GibbedEntity = gibbedEntity;
        GibberEntity = gibberEntity;
    }
}
