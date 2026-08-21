using Content.Shared.Actions.Components;
using Content.Shared.Body;
using Content.Shared.Body.Part;
using Robust.Shared.Containers;

namespace Content.Shared.Backmen.Flesh;

/// <summary>
/// Shared skip rules when dumping a corpse's containers (devour, flesh heart, mutation).
/// </summary>
public static class FleshCorpseContainerDump
{
    /// <summary>
    /// Action entities live here as HUD icons; emptying this container drops those icons on the floor.
    /// </summary>
    public static bool ShouldSkipContainer(BaseContainer container)
    {
        return container.ID == ActionsContainerComponent.ContainerId;
    }

    /// <summary>
    /// Skip action icons, organs, and body parts. Organs/parts are handled by
    /// <c>StripBodyForSkeleton</c> (head stays; the rest is deleted).
    /// </summary>
    public static bool ShouldSkipEntity(EntityUid ent, IEntityManager entities)
    {
        return entities.HasComponent<ActionComponent>(ent)
               || entities.HasComponent<OrganComponent>(ent)
               || entities.HasComponent<BodyPartComponent>(ent);
    }
}
