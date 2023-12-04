using Content.Server.Backmen.Shipwrecked.Components;
using Content.Server.Explosion.EntitySystems;
using Content.Server.Humanoid.Systems;
using Content.Server.RandomMetadata;
using Content.Server.Zombies;
using Content.Shared.Damage;
using Content.Shared.Weapons.Melee;
using Content.Shared.Zombies;
using Robust.Shared.Prototypes;

namespace Content.Server.Backmen.Shipwrecked;

public sealed class NpcZombieMakeEvent : EntityEventArgs
{
    public EntityUid Target { get; set; }
    public bool IsBoss { get; set; }
}


public sealed class NPCZombieSystem : EntitySystem
{
    [Dependency] private readonly ZombieSystem _zombieSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ZombifiedOnSpawnComponent, MapInitEvent>(OnSpawnZombifiedStartup, after: new []{ typeof(RandomMetadataSystem), typeof(RandomHumanoidSystem) });
        SubscribeLocalEvent<ZombieSurpriseComponent, MapInitEvent>(OnZombieSurpriseInit, after: new []{ typeof(RandomMetadataSystem), typeof(RandomHumanoidSystem) });
        SubscribeLocalEvent<ZombieWakeupOnTriggerComponent, TriggerEvent>(OnZombieWakeupTrigger);
        SubscribeLocalEvent<NpcZombieMakeEvent>(OnZombifyEntity);
    }

    private void OnZombifyEntity(NpcZombieMakeEvent ev)
    {
        if (TerminatingOrDeleted(ev.Target))
            return;

        _zombieSystem.ZombifyEntity(ev.Target);


        var z = EnsureComp<ZombieComponent>(ev.Target);
        z.MaxZombieInfectionChance = 0.0001f;
        z.MinZombieInfectionChance = 0.00001f;

        var melee = EnsureComp<MeleeWeaponComponent>(ev.Target); // npc (lower damage, like a flesh)
        melee.Angle = 0;
        melee.AttackRate = 1;
        melee.BluntStaminaDamageFactor = 0.5;
        melee.ClickDamageModifier = 1;
        melee.Range = 1.5f;



        if (!ev.IsBoss)
        {
            z.HealingOnBite = new();

            DamageSpecifier dspec = new()
            {
                DamageDict = new()
                {
                    { "Slash", 8 }
                }
            };
            melee.Damage = dspec;
            z.HealingOnBite = new();
            z.ZombieMovementSpeedDebuff = 0.60f;
            Dirty(ev.Target,melee);
        }
        else
        {
            DamageSpecifier dspec = new()
            {
                DamageDict = new()
                {
                    { "Slash", 14 },
                    { "Piercing", 6 }
                }
            };
            melee.Damage = dspec;
            Dirty(ev.Target,melee);
            DamageSpecifier hspec = new()
            {
                DamageDict = new()
                {
                    { "Blunt", -1 },
                    { "Slash", -1 },
                    { "Piercing", -1 }
                }
            };
            z.HealingOnBite = hspec;
        }
        Dirty(ev.Target,z);
    }

    private void OnSpawnZombifiedStartup(EntityUid uid, ZombifiedOnSpawnComponent component, MapInitEvent args)
    {
        if (TerminatingOrDeleted(uid))
            return;

        RemCompDeferred<ZombifiedOnSpawnComponent>(uid);
        QueueLocalEvent(new NpcZombieMakeEvent
        {
            Target = uid,
            IsBoss = component.IsBoss
        });
    }

    [ValidatePrototypeId<EntityPrototype>] private const string ZombieSurpriseDetector = "ZombieSurpriseDetector";
    private void OnZombieSurpriseInit(EntityUid uid, ZombieSurpriseComponent component, MapInitEvent args)
    {
        if (TerminatingOrDeleted(uid))
            return;

        // Spawn a separate collider attached to the entity.
        var trigger = Spawn(ZombieSurpriseDetector, Transform(uid).Coordinates);
        Comp<ZombieWakeupOnTriggerComponent>(trigger).ToZombify = uid;
    }

    private void OnZombieWakeupTrigger(EntityUid uid, ZombieWakeupOnTriggerComponent component, TriggerEvent args)
    {
        QueueDel(uid);

        var toZombify = component.ToZombify;
        if (toZombify == null || Deleted(toZombify))
            return;

        QueueLocalEvent(new NpcZombieMakeEvent
        {
            Target = toZombify.Value,
            IsBoss = false
        });
    }
}
