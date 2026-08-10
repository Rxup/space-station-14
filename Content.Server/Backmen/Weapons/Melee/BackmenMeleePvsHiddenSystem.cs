using System.Linq;
using System.Numerics;
using Content.Shared.Backmen.Weapons.Melee;
using Content.Shared.Eye;
using Content.Shared.Interaction;
using Content.Shared.Physics;
using Content.Shared.Weapons.Melee;
using Robust.Shared.Map;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Systems;

namespace Content.Server.Backmen.Weapons.Melee;

/// <summary>
/// Lets wide/heavy melee hit PVS-hidden entities (psionic invis / shadowkin DarkSwap) via server arc raycast.
/// Click/light/disarm still require a client-visible target.
/// </summary>
public sealed partial class BackmenMeleePvsHiddenSystem : EntitySystem
{
    private const int AttackMask = (int) (CollisionGroup.MobMask | CollisionGroup.Opaque);
    private const ushort HiddenVisMask =
        (ushort) (VisibilityFlags.PsionicInvisibility | VisibilityFlags.DarkSwapInvisibility);

    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private EntityQuery<MetaDataComponent> _metaQuery = default!;

    private readonly HashSet<Entity<VisibilityComponent>> _nearbyVis = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MeleeResolveHeavyTargetsEvent>(OnResolveHeavy);
    }

    private void OnResolveHeavy(ref MeleeResolveHeavyTargetsEvent args)
    {
        var distance = Math.Min(args.Range, args.Direction.Length());
        if (distance <= 0f)
            return;

        // Cheap broadphase: skip expensive arc rays unless a PVS-hidden target is nearby.
        if (!AnyHiddenNearby(args.User, args.Range))
            return;

        var hits = ArcRayCast(args.UserPosition, args.Direction.ToWorldAngle(), args.ArcWidth, distance, args.MapId, args.User);

        foreach (var hit in hits)
        {
            if (args.Entities.Contains(hit))
                continue;

            if (!_metaQuery.TryGetComponent(hit, out var meta) ||
                (meta.VisibilityMask & HiddenVisMask) == 0)
                continue;

            if (args.Entities.Count >= SharedMeleeWeaponSystem.MaxTargets)
                break;

            args.Entities.Add(hit);
        }
    }

    private bool AnyHiddenNearby(EntityUid user, float range)
    {
        _nearbyVis.Clear();
        var mapPos = _transform.GetMapCoordinates(user);
        _lookup.GetEntitiesInRange(mapPos, range, _nearbyVis);

        foreach (var (ent, vis) in _nearbyVis)
        {
            if (ent == user)
                continue;

            if ((vis.Layer & HiddenVisMask) != 0)
                return true;
        }

        return false;
    }

    private HashSet<EntityUid> ArcRayCast(Vector2 position, Angle angle, Angle arcWidth, float range, MapId mapId, EntityUid ignore)
    {
        var widthRad = arcWidth;
        var increments = 1 + 35 * (int) Math.Ceiling(widthRad / (2 * Math.PI));
        var increment = widthRad / increments;
        var baseAngle = angle - widthRad / 2;
        var resSet = new HashSet<EntityUid>();

        for (var i = 0; i < increments; i++)
        {
            var castAngle = new Angle(baseAngle + increment * i);
            var res = _physics.IntersectRay(mapId,
                    new CollisionRay(position, castAngle.ToWorldVec(), AttackMask),
                    range,
                    ignore,
                    false)
                .ToList();

            if (res.Count == 0)
                continue;

            var resChecked = res.Where(x => x.Distance.Equals(res[0].Distance));
            foreach (var r in resChecked)
            {
                if (_interaction.InRangeUnobstructed(ignore, r.HitEntity, range + 0.1f, overlapCheck: false))
                    resSet.Add(r.HitEntity);
            }
        }

        return resSet;
    }
}
