using Robust.Shared.Serialization;

namespace Content.Shared.Gibbing.Events;

/// <summary>
/// Called just before we actually gib the target entity
/// </summary>
[ByRefEvent]
public record struct AttemptEntityContentsGibEvent(
    EntityUid Target,
    GibContentsOption GibType,
    List<string>? AllowedContainers,
    List<string>? ExcludedContainers
);

/// <summary>
/// Called just before we actually gib the target entity
/// </summary>
[ByRefEvent]
public record struct AttemptEntityGibEvent(EntityUid Target, int GibletCount, GibType GibType);

/// <summary>
/// Called immediately after we gib the target entity
/// </summary>
[ByRefEvent]
public record struct EntityGibbedEvent(EntityUid Target, List<EntityUid> DroppedEntities);

[Serializable, NetSerializable]
public enum GibType : byte
{
    Skip,
    Drop,
    Gib,
}

public enum GibContentsOption : byte
{
    Skip,
    Drop,
    Gib
}
