using Content.Server.Backmen.Language;
using Content.Server.Backmen.Shipwrecked.Components;
using Content.Shared.Backmen.Shipwrecked;
using Content.Server.Backmen.Surgery.Consciousness.Systems;
using Content.Server.Ghost.Roles.Components;
using Content.Server.Humanoid.Systems;
using Content.Server.RandomMetadata;
using Content.Server.Zombies;
using Content.Shared.Backmen.Language.Components;
using Content.Shared.Backmen.Language.Systems;
using Content.Shared.Backmen.Standing;
using Content.Shared.Backmen.Surgery.Consciousness.Components;
using Content.Shared.Bed.Sleep;
using Content.Shared.Body;
using Content.Shared.Body.Events;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.SSDIndicator;
using Content.Shared.Standing;
using Content.Shared.Stunnable;
using Content.Shared.StatusEffectNew;
using Content.Shared.Trigger;
using Content.Shared.Trigger.Components.Triggers;
using Content.Shared.Weapons.Melee;
using Content.Shared.Zombies;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using System.Linq;
using Robust.Shared.Utility;

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
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private MobThresholdSystem _mobThresholds = default!;
    [Dependency] private ServerConsciousnessSystem _consciousness = default!;
    [Dependency] private StatusEffectsSystem _status = default!;
    [Dependency] private SharedStunSystem _stun = default!;
    [Dependency] private SharedLayingDownSystem _layingDown = default!;
    [Dependency] private SSDIndicatorSystem _ssdIndicator = default!;
    [Dependency] private EntityQuery<ZombieSurpriseComponent> _zombieSurpriseQuery = default!;
    [Dependency] private EntityQuery<ZombieComponent> _zombieQuery = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ZombifiedOnSpawnComponent, MapInitEvent>(OnSpawnZombifiedStartup, after: new []{ typeof(RandomMetadataSystem), typeof(RandomHumanoidSystem) });
        SubscribeLocalEvent<ZombifiedOnSpawnComponent, InitialBodySpawnedEvent>(OnZombifiedBodyReady, after: [typeof(ServerConsciousnessSystem)]);
        SubscribeLocalEvent<ZombieSurpriseComponent, InitialBodySpawnedEvent>(OnZombieSurpriseBodyReady, after: [typeof(ServerConsciousnessSystem)]);
        SubscribeLocalEvent<ZombieSurpriseComponent, MapInitEvent>(OnZombieSurpriseMapInit, after: new []{ typeof(RandomMetadataSystem), typeof(RandomHumanoidSystem) });
        SubscribeLocalEvent<ZombieWakeupOnTriggerComponent, TriggerEvent>(OnZombieWakeupTrigger);
        SubscribeLocalEvent<NpcZombieMakeEvent>(OnZombifyEntity);
        // triggerSpeed: 0 treats motionless mobs as valid; NPCZombieSystem ignores posed corpses and other zombies.
        SubscribeLocalEvent<ZombieWakeupOnTriggerComponent, AttemptTriggerEvent>(OnZombieWakeupAttemptTrigger);
    }

    private void OnZombieSurpriseMapInit(EntityUid uid, ZombieSurpriseComponent _, MapInitEvent args)
    {
        if (HasComp<InitialBodyComponent>(uid))
            return;

        SetupZombieSurprise(uid);
    }

    private void OnZombieSurpriseBodyReady(EntityUid uid, ZombieSurpriseComponent _, InitialBodySpawnedEvent args)
    {
        SetupZombieSurprise(uid);
    }

    private void OnZombieWakeupAttemptTrigger(EntityUid uid, ZombieWakeupOnTriggerComponent component, ref AttemptTriggerEvent args)
    {
        if (args.User == null || !ShouldIgnoreProximityUser(args.User.Value))
            return;

        args.Cancelled = true;
    }

    /// <summary>
    /// Posed surprise corpses and existing zombies must not trigger each other's proximity detectors.
    /// </summary>
    private bool ShouldIgnoreProximityUser(EntityUid user)
    {
        return _zombieSurpriseQuery.HasComp(user) || _zombieQuery.HasComp(user);
    }

    private void OnZombifyEntity(NpcZombieMakeEvent ev)
    {
        if (TerminatingOrDeleted(ev.Target))
            return;

        RemComp<ZombieSurpriseComponent>(ev.Target);
        _mobThresholds.SetAllowRevives(ev.Target, true);

        _zombieSystem.ZombifyEntity(ev.Target);
        ClearMobStunAndStand(ev.Target);

        var target = ev.Target;
        Timer.Spawn(TimeSpan.Zero, () =>
        {
            if (!TerminatingOrDeleted(target))
                ClearMobStunAndStand(target);
        });

        EnsureComp<UniversalLanguageSpeakerComponent>(ev.Target);
        _language.SetLanguage(ev.Target, SharedLanguageSystem.Universal);
        RemComp<GhostTakeoverAvailableComponent>(ev.Target);
        RemComp<GhostRoleComponent>(ev.Target);
        RemComp<ConsciousnessComponent>(ev.Target);

        var injurable = EnsureComp<InjurableComponent>(ev.Target);
        injurable.DamageContainer = "Biological";

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

    /// <summary>
    /// Clears stun from mobs on the planet map after the crash.
    /// Zombies are also forced upright; posed surprise corpses only lose stun.
    /// </summary>
    public void ClearStunOnMap(MapId? mapId)
    {
        if (mapId == null || mapId == MapId.Nullspace)
            return;

        RunClearStunOnMap(mapId.Value);
        Timer.Spawn(TimeSpan.Zero, () => RunClearStunOnMap(mapId.Value));
    }

    private void RunClearStunOnMap(MapId mapId)
    {
        var query = AllEntityQuery<MobStateComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var xform))
        {
            if (xform.MapID != mapId)
                continue;

            ClearEntityStun(uid);
        }
    }

    private void ClearMobStunAndStand(EntityUid uid)
    {
        ClearEntityStun(uid, forceStand: true);
    }

    /// <summary>
    /// Corpses and sleep/knockdown can leave a bare <see cref="StunnedComponent"/> without visuals.
    /// Status-effect removal is deferred, so we also clear again on the next tick.
    /// </summary>
    private void ClearEntityStun(EntityUid uid, bool? forceStand = null)
    {
        if (TerminatingOrDeleted(uid))
            return;

        if (HasComp<SleepingComponent>(uid))
            RemComp<SleepingComponent>(uid);

        _status.TryRemoveStatusEffect(uid, SharedStunSystem.StunId);
        _status.TryRemoveStatusEffect(uid, SSDIndicatorSystem.StatusEffectSSDSleeping);
        _ssdIndicator.ClearSsdSleep(uid);

        if (TryComp<KnockedDownComponent>(uid, out var knocked))
        {
            _stun.CancelKnockdownDoAfter((uid, knocked));
            RemComp<KnockedDownComponent>(uid);
        }

        _stun.TryUnstun(uid);
        RemComp<StunnedComponent>(uid);

        var shouldStand = forceStand ?? _zombieQuery.HasComp(uid);
        if (!shouldStand)
            return;

        if (!TryComp<StandingStateComponent>(uid, out var standing) || !_stateSystem.IsDown((uid, standing)))
            return;

        if (TryComp<LayingDownComponent>(uid, out var laying))
            _layingDown.TryStandUp(uid, laying, standing);
        else
            _stateSystem.Stand(uid, standing, force: true);
    }

    private void OnSpawnZombifiedStartup(EntityUid uid, ZombifiedOnSpawnComponent component, MapInitEvent args)
    {
        if (HasComp<InitialBodyComponent>(uid))
            return;

        QueueZombify(uid, component.IsBoss);
    }

    private void OnZombifiedBodyReady(EntityUid uid, ZombifiedOnSpawnComponent component, InitialBodySpawnedEvent args)
    {
        QueueZombify(uid, component.IsBoss);
    }

    private void QueueZombify(EntityUid uid, bool isBoss)
    {
        if (TerminatingOrDeleted(uid))
            return;

        RemCompDeferred<ZombifiedOnSpawnComponent>(uid);
        QueueLocalEvent(new NpcZombieMakeEvent
        {
            Target = uid,
            IsBoss = isBoss
        });
    }

    public void SetupZombieSurprise(EntityUid uid)
    {
        if (TerminatingOrDeleted(uid) || !HasComp<MobStateComponent>(uid))
            return;

        EnsureDetector(uid);
        EnsureCorpsePose(uid);
    }

    private void EnsureDetector(EntityUid uid)
    {
        var existingDetector = AllEntityQuery<ZombieWakeupOnTriggerComponent>();
        while (existingDetector.MoveNext(out _, out var wakeup))
        {
            if (wakeup.ToZombify == uid)
                return;
        }

        var trigger = Spawn(ZombieSurpriseDetector, Transform(uid).Coordinates);
        Comp<ZombieWakeupOnTriggerComponent>(trigger).ToZombify = uid;

        if (TryComp<TriggerOnProximityComponent>(trigger, out var proximity))
        {
            foreach (var entity in proximity.Colliding.Keys.ToArray())
            {
                if (ShouldIgnoreProximityUser(entity))
                    proximity.Colliding.Remove(entity);
            }
        }
    }

    public void EnsureCorpsePose(EntityUid uid)
    {
        if (TerminatingOrDeleted(uid) || !TryComp<MobStateComponent>(uid, out var mobState))
            return;

        _consciousness.ApplyForcedCorpseState(uid);
        _mobThresholds.VerifyThresholds(uid);
        _mobThresholds.SetAllowRevives(uid, false);
        _mobState.ChangeMobState(uid, MobState.Dead, mobState);
        _stateSystem.Down(uid, playSound: false, force: true);
    }

    private static readonly EntProtoId ZombieSurpriseDetector = "ZombieSurpriseDetector";

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
