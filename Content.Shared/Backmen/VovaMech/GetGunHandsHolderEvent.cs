using Robust.Shared.GameObjects;

namespace Content.Shared.Backmen.VovaMech;

/// <summary>
/// Raised directed at an entity to resolve which entity holds guns for ranged combat.
/// </summary>
[ByRefEvent]
public struct GetGunHandsHolderEvent
{
    public EntityUid Entity;
    public EntityUid Holder;

    public GetGunHandsHolderEvent(EntityUid entity)
    {
        Entity = entity;
        Holder = entity;
    }
}
