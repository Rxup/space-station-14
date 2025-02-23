using Content.Server.Popups;
using Content.Shared.Projectiles;
using Content.Shared.StatusEffect;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.Magic;

public sealed partial class ChainFireballSystem : EntitySystem
{
    [Dependency] private readonly SharedGunSystem _gun = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly PhysicsSystem _physics = default!;
    [Dependency] private readonly IMapManager _mapMan = default!;
    [Dependency] private readonly MapSystem _mapSystem = default!;

    private EntityQuery<ChainFireballComponent> _fireballQuery;
    private EntityQuery<StatusEffectsComponent> _statusQuery;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ChainFireballComponent, ProjectileHitEvent>(OnHit);

        _fireballQuery = GetEntityQuery<ChainFireballComponent>();
        _statusQuery = GetEntityQuery<StatusEffectsComponent>();
    }

    private void OnHit(Entity<ChainFireballComponent> ent, ref ProjectileHitEvent args)
    {
        if (_random.Prob(ent.Comp.DisappearChance))
            return;

        Spawn(ent, ent.Comp.IgnoredTargets);

        QueueDel(ent);
    }

    public bool Spawn(EntityUid source, List<EntityUid> ignoredTargets)
    {
        var lookup = _lookup.GetEntitiesInRange(source, 5f);

        List<EntityUid> mobs = new();
        foreach (var look in lookup)
        {
            if (ignoredTargets.Contains(look)
                || !_statusQuery.HasComp(look)) // ignore non mobs whatsoever
                continue;

            mobs.Add(look);
        }

        if (mobs.Count == 0)
        {
            _popup.PopupEntity(Loc.GetString("heretic-ability-fail-notarget"), source, source);
            return false;
        }

        return Spawn(source, mobs[_random.Next(0, mobs.Count - 1)], ignoredTargets);
    }

    public bool Spawn(EntityUid source, EntityUid target, List<EntityUid> ignoredTargets)
    {
        return SpawnFireball(source, target, ignoredTargets);
    }


    [ValidatePrototypeId<EntityPrototype>]
    private const string FireballChain = "FireballChain";

    private bool SpawnFireball(EntityUid uid, EntityUid target, List<EntityUid> ignoredTargets)
    {
        var ball = Spawn(FireballChain, Transform(uid).Coordinates);
        if (_fireballQuery.TryComp(ball, out var sfc))
        {
            sfc.IgnoredTargets = sfc.IgnoredTargets.Count > 0 ? sfc.IgnoredTargets : ignoredTargets;

            if (_fireballQuery.TryComp(uid, out var usfc))
                sfc.DisappearChance = usfc.DisappearChance + sfc.DisappearChanceDelta;
        }

        // launch it towards the target
        var fromCoords = Transform(uid).Coordinates;
        var toCoords = Transform(target).Coordinates;
        var userVelocity = _physics.GetMapLinearVelocity(uid);

        // If applicable, this ensures the projectile is parented to grid on spawn, instead of the map.
        var fromMap = _transform.ToMapCoordinates(fromCoords);

        if (!_mapSystem.TryGetMap(fromMap.MapId, out var spawnEntId))
        {
            return false;
        }

        var spawnCoords = _mapMan.TryFindGridAt(fromMap, out var gridUid, out _)
            ? _transform.WithEntityId(fromCoords, gridUid)
            : new(spawnEntId.Value, fromMap.Position);

        var direction = _transform.ToMapCoordinates(toCoords).Position -
                        _transform.ToMapCoordinates(spawnCoords).Position;

        _gun.ShootProjectile(ball, direction, userVelocity, uid, ball);

        return true;
    }
}
