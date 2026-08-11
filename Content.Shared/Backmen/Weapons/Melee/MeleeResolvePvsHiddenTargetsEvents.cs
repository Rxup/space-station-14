using System.Numerics;
using Robust.Shared.Map;
using Robust.Shared.Player;

namespace Content.Shared.Backmen.Weapons.Melee;

/// <summary>
/// Raised on the attacker before a heavy melee swing validates its hit list.
/// Handlers may append physically-hit entities the client never saw (psi invis / shadowkin DarkSwap).
/// Click/light/disarm attacks are intentionally not covered.
/// </summary>
[ByRefEvent]
public struct MeleeResolveHeavyTargetsEvent
{
    public EntityUid User;
    public EntityUid Weapon;
    public EntityCoordinates Coordinates;
    public float Range;
    public Angle ArcWidth;
    public Vector2 UserPosition;
    public Vector2 Direction;
    public MapId MapId;
    public ICommonSession? Session;
    public List<EntityUid> Entities;

    public MeleeResolveHeavyTargetsEvent(
        EntityUid user,
        EntityUid weapon,
        EntityCoordinates coordinates,
        float range,
        Angle arcWidth,
        Vector2 userPosition,
        Vector2 direction,
        MapId mapId,
        ICommonSession? session,
        List<EntityUid> entities)
    {
        User = user;
        Weapon = weapon;
        Coordinates = coordinates;
        Range = range;
        ArcWidth = arcWidth;
        UserPosition = userPosition;
        Direction = direction;
        MapId = mapId;
        Session = session;
        Entities = entities;
    }
}
