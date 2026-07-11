using Robust.Shared.Serialization;

namespace Content.Shared._Lavaland.Aggression;

/// <summary>
/// Raised on the entity with AggressiveComponent when it added new aggressor.
/// </summary>
[DataDefinition]
[Serializable, NetSerializable]
public sealed partial class AggressorAddedEvent : EntityEventArgs
{
    [DataField] public NetEntity Aggressor;

    public AggressorAddedEvent(NetEntity added)
    {
        Aggressor = added;
    }
}

/// <summary>
/// Raised on the entity with AggressiveComponent when it removed one of it's aggressors.
/// </summary>
[DataDefinition]
[Serializable, NetSerializable]
public sealed partial class AggressorRemovedEvent : EntityEventArgs
{
    [DataField] public NetEntity Aggressor;

    public AggressorRemovedEvent(NetEntity removed)
    {
        Aggressor = removed;
    }
}
