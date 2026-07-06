using Content.Shared.Backmen.Supermatter.Components;
using Content.Shared.Backmen.Supermatter.Events;
using Content.Shared.Clothing.Components;
using Content.Shared.Examine;
using Content.Shared.Inventory;
using Content.Shared.Mobs.Components;
using Content.Shared.Projectiles;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Network;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Events;

namespace Content.Shared.Backmen.Supermatter;

public abstract partial class SharedSupermatterSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BkmSupermatterComponent, ExaminedEvent>(OnExamine);
        SubscribeLocalEvent<BkmSupermatterImmuneComponent, ExaminedEvent>(OnImmuneExamined);
        SubscribeLocalEvent<BkmSupermatterImmuneComponent, BkmSupermatterImmuneEvent>(OnIfImmune);
        SubscribeLocalEvent<BkmSupermatterImmuneComponent, InventoryRelayedEvent<BkmSupermatterImmuneEvent>>(OnIfImmuneItem);
        SubscribeLocalEvent<InventoryComponent, BkmSupermatterImmuneEvent>(RelayInventoryEvent);
        SubscribeLocalEvent<BkmSupermatterComponent, StartCollideEvent>(OnCollideEvent, before: [typeof(SharedProjectileSystem)]);
    }

    private void OnIfImmuneItem(Entity<BkmSupermatterImmuneComponent> ent, ref InventoryRelayedEvent<BkmSupermatterImmuneEvent> args)
    {
        args.Args.Cancel();
    }

    private void RelayInventoryEvent(Entity<InventoryComponent> ent, ref BkmSupermatterImmuneEvent args)
    {
        _inventory.RelayEvent(ent, args);
    }

    private void OnIfImmune(Entity<BkmSupermatterImmuneComponent> ent, ref BkmSupermatterImmuneEvent args)
    {
        args.Cancel();
    }

    private void OnImmuneExamined(Entity<BkmSupermatterImmuneComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        var locId = HasComp<ClothingComponent>(ent)
            ? "supermatter-examine-immune-clothing"
            : "supermatter-examine-immune-entity";

        args.PushMarkup(Loc.GetString(locId));
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
            || _container.IsEntityInContainer(uid))
            return;

        if (IsSupermatterImmune(target, uid))
            return;

        if (_projectileQuery.TryComp(target, out var projectile))
        {
            if (_net.IsServer)
            {
                supermatter.Power += (float) projectile.Damage.GetTotal();
                _audio.PlayPvs(supermatter.ProjectileHitSound, uid);
                var projectileHitEv = new BkmSupermatterProjectileHitEvent();
                RaiseLocalEvent(uid, ref projectileHitEv);
            }

            PredictedQueueDel(target);
            return;
        }

        if (!_net.IsServer)
            return;

        if (_foodQuery.TryComp(target, out var supermatterFood))
            supermatter.Power += supermatterFood.Energy;
        else
            supermatter.Power++;

        supermatter.MatterPower += _mobState.HasComp(target) ? 200 : 0;
        DustEntity(uid, supermatter, target);
    }

    protected bool IsSupermatterImmune(EntityUid target, EntityUid source)
    {
        if (SupermatterImmuneQuery.HasComp(target) || SupermatterImmuneQuery.HasComp(source))
            return true;

        var ev = new BkmSupermatterImmuneEvent(target, source);
        RaiseLocalEvent(target, ev);
        RaiseLocalEvent(source, ev);
        return ev.Cancelled;
    }

    /// <summary>
    /// Server-only. Spawns ash, plays burn sounds, deletes entities.
    /// </summary>
    protected virtual void DustEntity(EntityUid uid, BkmSupermatterComponent supermatter, EntityUid target)
    {
    }

    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private EntityQuery<ProjectileComponent> _projectileQuery = default;
    [Dependency] private EntityQuery<MobStateComponent> _mobState = default;
    [Dependency] private EntityQuery<BkmSupermatterFoodComponent> _foodQuery = default;
    [Dependency] protected EntityQuery<BkmSupermatterImmuneComponent> SupermatterImmuneQuery = default;
}
