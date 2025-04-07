using Robust.Shared.GameObjects;

namespace Content.Server.Backmen.GibOnCollide;

/// <summary>
///  Это событие вызывается, когда сущность гибнет через GibOnTouchSystem.
/// </summary>
public sealed class GibOnCollideAttemptEvent : EntityEventArgs
{
    public EntityUid GibbedEntity { get; }
    public EntityUid GibberEntity { get; }

    public GibOnTouchAttemptEvent(EntityUid gibbedEntity, EntityUid gibberEntity)
    {
        GibbedEntity = gibbedEntity;
        GibberEntity = gibberEntity;
    }
}
