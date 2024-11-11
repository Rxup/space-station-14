using Content.Shared.Backmen.Supermatter.Components;
using Content.Shared.Examine;
using Content.Shared.Mobs.Components;
using Content.Shared.Projectiles;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Events;
using Robust.Shared.Prototypes;

namespace Content.Shared.Backmen.Supermatter;

public abstract class SharedSupermatterSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BkmSupermatterComponent, ExaminedEvent>(OnExamine);
        SubscribeLocalEvent<BkmSupermatterComponent, StartCollideEvent>(OnCollideEvent);


        _projectileQuery = GetEntityQuery<ProjectileComponent>();
        _supermatterImmuneQuery = GetEntityQuery<BkmSupermatterImmuneComponent>();
    }

    protected float GetIntegrity(float damage, float explosionPoint)
    {
        var integrity = damage / explosionPoint;
        integrity = (float) Math.Round(100 - integrity * 100, 2);
        integrity = integrity < 0 ? 0 : integrity;
        return integrity;
    }

    protected float GetIntegrity(Entity<BkmSupermatterComponent> ent)
    {
        return GetIntegrity(ent.Comp.Damage, ent.Comp.ExplosionPoint);
    }

    private void OnExamine(Entity<BkmSupermatterComponent> ent, ref ExaminedEvent args)
    {
        if (args.IsInDetailsRange)
            args.PushMarkup(Loc.GetString("supermatter-examine-integrity", ("integrity", GetIntegrity(ent).ToString("0.00"))));
    }

    private void OnCollideEvent(EntityUid uid, BkmSupermatterComponent supermatter, ref StartCollideEvent args)
    {
        var target = args.OtherEntity;
        if (args.OtherBody.BodyType == BodyType.Static
            || _supermatterImmuneQuery.HasComp(target)
            || _container.IsEntityInContainer(uid))
            return;

        if (TryComp<BkmSupermatterFoodComponent>(target, out var supermatterFood))
            supermatter.Power += supermatterFood.Energy;
        else if (_projectileQuery.TryComp(target, out var projectile))
            supermatter.Power += (float) projectile.Damage.GetTotal();
        else
            supermatter.Power++;

        supermatter.MatterPower += HasComp<MobStateComponent>(target) ? 200 : 0;
        if (!_projectileQuery.HasComp(target))
        {
            Spawn(Ash, Transform(target).Coordinates);
            _audio.PlayPvs(supermatter.DustSound, uid);
            QueueDel(target);
        }
    }


    [ValidatePrototypeId<EntityPrototype>]
    protected const string Ash = "Ash";


    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;

    private EntityQuery<ProjectileComponent> _projectileQuery;
    protected EntityQuery<BkmSupermatterImmuneComponent> _supermatterImmuneQuery;
}
