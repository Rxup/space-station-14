using Content.Server.Backmen.Shipwrecked.Components;
using Content.Server.Explosion.EntitySystems;
using Content.Server.NPC;
using Content.Server.NPC.HTN;
using Content.Server.NPC.Systems;
using Content.Server.Zombies;
using Content.Shared.Damage;
using Content.Shared.Humanoid;
using Content.Shared.Tools.Components;
using Content.Shared.Weapons.Melee;
using Content.Shared.Zombies;
using Robust.Server.GameObjects;
using Robust.Server.Physics;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Server.Backmen.Shipwrecked;

public sealed class NPCZombieSystem : EntitySystem
{
    [Dependency] private readonly ZombieSystem _zombieSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ZombifiedOnSpawnComponent, ComponentStartup>(OnSpawnZombifiedStartup);
        SubscribeLocalEvent<ZombieSurpriseComponent, ComponentInit>(OnZombieSurpriseInit);
        SubscribeLocalEvent<ZombieWakeupOnTriggerComponent, TriggerEvent>(OnZombieWakeupTrigger);
    }

    private void ZombifyEntity(EntityUid uid, bool isBoss)
    {


        _zombieSystem.ZombifyEntity(uid);


        var z = EnsureComp<ZombieComponent>(uid);
        z.MaxZombieInfectionChance = 0.0001f;
        z.MinZombieInfectionChance = 0.00001f;

        var melee = EnsureComp<MeleeWeaponComponent>(uid); // npc (lower damage, like a flesh)
        melee.Angle = 0;
        melee.AttackRate = 1;
        melee.BluntStaminaDamageFactor = 0.5;
        melee.ClickDamageModifier = 1;
        melee.Range = 1.5f;



        if (!isBoss)
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
            Dirty(uid,melee);
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
            Dirty(uid,melee);
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
        Dirty(uid,z);
    }

    private void OnSpawnZombifiedStartup(EntityUid uid, ZombifiedOnSpawnComponent component, ComponentStartup args)
    {
        RemCompDeferred<ZombifiedOnSpawnComponent>(uid);
        ZombifyEntity(uid, component.IsBoss);
    }

    [ValidatePrototypeId<EntityPrototype>] private const string ZombieSurpriseDetector = "ZombieSurpriseDetector";
    private void OnZombieSurpriseInit(EntityUid uid, ZombieSurpriseComponent component, ComponentInit args)
    {
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

        ZombifyEntity(toZombify.Value, false);
    }
}
