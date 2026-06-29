using Content.Server.Backmen.Language;
using Content.Server.Backmen.Shipwrecked.Components;
using Content.Server.Ghost.Roles.Components;
using Content.Server.Humanoid.Systems;
using Content.Server.RandomMetadata;
using Content.Server.Zombies;
using Content.Shared.Backmen.Language.Components;
using Content.Shared.Backmen.Language.Systems;
using Content.Shared.Backmen.Surgery.Consciousness.Components;
using Content.Shared.Damage;
using Content.Shared.Standing;
using Content.Shared.Stunnable;
using Content.Shared.Trigger;
using Content.Shared.Weapons.Melee;
using Content.Shared.Zombies;
using Robust.Shared.Prototypes;

namespace Content.Server.Backmen.Shipwrecked;

public sealed class NpcZombieMakeEvent : EntityEventArgs
{
    public EntityUid Target { get; set; }
    public bool IsBoss { get; set; }
}


public sealed partial class NPCZombieSystem : EntitySystem
{
    [Dependency] private ZombieSystem _zombieSystem = default!;
    [Dependency] private StandingStateSystem _stateSystem = default!;
    [Dependency] private LanguageSystem _language = default!;

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

        _stateSystem.Stand(ev.Target);
        _zombieSystem.ZombifyEntity(ev.Target);
        EnsureComp<UniversalLanguageSpeakerComponent>(ev.Target);
        _language.SetLanguage(ev.Target, SharedLanguageSystem.Universal);
        RemComp<GhostTakeoverAvailableComponent>(ev.Target);
        RemComp<GhostRoleComponent>(ev.Target);
        RemComp<ConsciousnessComponent>(ev.Target);
        RemComp<StunnedComponent>(ev.Target);

        var z = EnsureComp<ZombieComponent>(ev.Target);
        z.BaseZombieInfectionChance = 0.0001f;
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

    private static readonly EntProtoId ZombieSurpriseDetector = "ZombieSurpriseDetector";
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
