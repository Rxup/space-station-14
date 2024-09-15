using System.Linq;
using System.Numerics;
using Content.Server.Emp;
using Content.Server.Explosion.EntitySystems;
using Content.Shared.Backmen.Blob;
using Content.Shared.Backmen.Blob.Components;
using Content.Shared.Damage;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Weapons.Melee.Events;
using Robust.Server.GameObjects;
using Robust.Shared.Map.Components;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Backmen.Blob;

public sealed class BlobbernautSystem : SharedBlobbernautSystem
{
    [Dependency] private readonly EntityLookupSystem _entityLookupSystem = default!;
    [Dependency] private readonly DamageableSystem _damageableSystem = default!;

    [Dependency] private readonly ExplosionSystem _explosionSystem = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly EmpSystem _empSystem = default!;
    [Dependency] private readonly MobStateSystem _mobStateSystem = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    [Dependency] private readonly IGameTiming _gameTiming = default!;

    [Dependency] private readonly TransformSystem _transform = default!;
    //private EntityQuery<MapGridComponent> _mapGridQuery;
    //private EntityQuery<BlobTileComponent> _tileQuery;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BlobbernautComponent, MeleeHitEvent>(OnMeleeHit);

        //_mapGridQuery = GetEntityQuery<MapGridComponent>();
        //_tileQuery = GetEntityQuery<BlobTileComponent>();
    }


    private readonly HashSet<Entity<BlobTileComponent>> _entitySet = new();
    public override void Update(float frameTime)
    {
        base.Update(frameTime);


        var blobFactoryQuery = EntityQueryEnumerator<BlobbernautComponent, MobStateComponent>();
        while (blobFactoryQuery.MoveNext(out var ent, out var comp, out var mobStateComponent))
        {
            if (_mobStateSystem.IsDead(ent,mobStateComponent))
                continue;

            if (_gameTiming.CurTime < comp.NextDamage)
                continue;

            if (TerminatingOrDeleted(comp.Factory))
            {
                TryChangeDamage("blobberaut-factory-destroy", ent, comp.Damage);
                comp.NextDamage = _gameTiming.CurTime + TimeSpan.FromSeconds(comp.DamageFrequency);
                continue;
            }

            var xform = Transform(ent);

            if (xform.GridUid == null)
                continue;

            var mapPos = _transform.ToMapCoordinates(xform.Coordinates);

            _entitySet.Clear();
            _entityLookupSystem.GetEntitiesInRange(mapPos.MapId, mapPos.Position, 1f, _entitySet);

            if(_entitySet.Count != 0)
                continue;

            TryChangeDamage("blobberaut-not-on-blob-tile", ent, comp.Damage);
            comp.NextDamage = _gameTiming.CurTime + TimeSpan.FromSeconds(comp.DamageFrequency);
        }
    }

    private void OnMeleeHit(EntityUid uid, BlobbernautComponent component, MeleeHitEvent args)
    {
        if (args.HitEntities.Count >= 1)
            return;
        if (!TryComp<BlobTileComponent>(component.Factory, out var blobTileComponent))
            return;
        if (!TryComp<BlobCoreComponent>(blobTileComponent.Core, out var blobCoreComponent))
            return;
        if (blobCoreComponent.CurrentChem == BlobChemType.ExplosiveLattice)
        {
            _explosionSystem.QueueExplosion(args.HitEntities.FirstOrDefault(), blobCoreComponent.BlobExplosive, 4, 1, 2, maxTileBreak: 0);
        }
        if (blobCoreComponent.CurrentChem == BlobChemType.ElectromagneticWeb)
        {
            var xform = Transform(args.HitEntities.FirstOrDefault());
            if (_random.Prob(0.2f))
                _empSystem.EmpPulse(_transform.GetMapCoordinates(xform), 3f, 50f, 3f);
        }
    }

    private DamageSpecifier? TryChangeDamage(string msg, EntityUid ent, DamageSpecifier dmg)
    {
        _popup.PopupEntity(Loc.GetString(msg), ent, ent, PopupType.LargeCaution);
        return _damageableSystem.TryChangeDamage(ent, dmg);
    }
}
