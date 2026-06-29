using Content.Server.NPC;
using Content.Server.NPC.HTN;
using Content.Server.Construction.Components;
using Content.Server._Impstation.Revenant.Components;
using Content.Server.Resist;
using Content.Shared.CombatMode;
using Content.Shared.Cuffs.Components;
using Content.Shared.Interaction;
using Content.Shared.Trigger.Components.Triggers;
using Content.Shared.DoAfter;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Explosion.Components;
using Content.Shared.Item.ItemToggle;
using Content.Shared.Item.ItemToggle.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.NPC.Components;
using Content.Shared.NPC.Prototypes;
using Content.Shared.NPC.Systems;
using Content.Shared.Popups;
using Content.Shared.Revenant.Components;
using Content.Shared.Trigger.Components;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Impstation.Revenant.EntitySystems;

public sealed partial class RevenantAnimatedSystem : EntitySystem
{
    private static readonly ProtoId<DamageTypePrototype> BluntDamage = "Blunt";
    private static readonly ProtoId<NpcFactionPrototype> SimpleHostileFaction = "SimpleHostile";

    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private NpcFactionSystem _factionSystem = default!;
    [Dependency] private IPrototypeManager _prototypeManager = default!;
    [Dependency] private ItemToggleSystem _itemToggleSystem = default!;
    [Dependency] private SharedGunSystem _gunSystem = default!;
    [Dependency] private MovementSpeedModifierSystem _moveSpeed = default!;
    [Dependency] private MobThresholdSystem _thresholds = default!;
    [Dependency] private IGameTiming _gameTiming = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RevenantAnimatedComponent, ComponentStartup>(OnComponentStartup);
        SubscribeLocalEvent<RevenantAnimatedComponent, ComponentShutdown>(OnComponentShutdown);
        SubscribeLocalEvent<RevenantAnimatedComponent, MobStateChangedEvent>(OnMobStateChange);
        SubscribeLocalEvent<RevenantAnimatedComponent, GetUsedEntityEvent>(OnGetUsedEntity);
    }

    private void OnGetUsedEntity(Entity<RevenantAnimatedComponent> ent, ref GetUsedEntityEvent args)
    {
        if (args.User != ent.Owner)
            return;

        if (HasComp<HandcuffComponent>(ent) || HasComp<TriggerOnUseComponent>(ent))
            args.Used = ent.Owner;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var enumerator = EntityQueryEnumerator<RevenantAnimatedComponent>();

        while (enumerator.MoveNext(out var uid, out var animate))
        {
            if (animate.EndTime == null)
                continue;

            if (animate.EndTime <= _gameTiming.CurTime)
                InanimateTarget(uid, animate);
        }
    }

    private void OnComponentStartup(EntityUid uid, RevenantAnimatedComponent comp, ComponentStartup args)
    {
        if (comp.LifeStage != ComponentLifeStage.Starting)
            return;

        if (HasComp<ItemToggleMeleeWeaponComponent>(uid) && TryComp<ItemToggleComponent>(uid, out var toggle))
            _itemToggleSystem.TryActivate((uid, toggle));

        _popup.PopupEntity(Loc.GetString("revenant-animate-item-animate", ("target", uid)), uid, Filter.Pvs(uid), true);

        if (EnsureHelper<MeleeWeaponComponent>(uid, comp, out var melee))
            melee.Damage = new DamageSpecifier(_prototypeManager.Index(BluntDamage), 5);

        EnsureHelper<CombatModeComponent>(uid, comp);
        EnsureHelper<MovementSpeedModifierComponent>(uid, comp, out var moveSpeed);
        if (comp.Revenant != null && TryComp<RevenantComponent>(comp.Revenant, out var revenant))
        {
            _moveSpeed.ChangeBaseSpeed(uid,
                revenant.AnimateWalkSpeed,
                revenant.AnimateSprintSpeed,
                MovementSpeedModifierComponent.DefaultAcceleration,
                moveSpeed);
        }
        else
        {
            _moveSpeed.ChangeBaseSpeed(uid,
                RevenantComponent.DefaultAnimateWalkSpeed,
                RevenantComponent.DefaultAnimateSprintSpeed,
                MovementSpeedModifierComponent.DefaultAcceleration,
                moveSpeed);
        }

        EnsureHelper<NpcFactionMemberComponent>(uid, comp, out var factions);
        _factionSystem.ClearFactions((uid, factions));
        _factionSystem.AddFaction((uid, factions), SimpleHostileFaction);

        EnsureHelper<MobStateComponent>(uid, comp);

        if (EnsureHelper<HTNComponent>(uid, comp, out var htn))
        {
            if (HasComp<GunComponent>(uid))
            {
                if (comp.Revenant != null
                    && TryComp<RevenantComponent>(comp.Revenant, out var rev)
                    && rev.AnimateCanBoltGuns
                    && TryComp<ChamberMagazineAmmoProviderComponent>(uid, out var chamber))
                {
                    _gunSystem.SetBoltClosed(uid, chamber, true);
                }

                htn.RootTask = new HTNCompoundTask { Task = "SimpleRangedHostileCompound" };
            }
            else if (HasComp<HandcuffComponent>(uid))
            {
                EnsureHelper<DoAfterComponent>(uid, comp);
                htn.RootTask = new HTNCompoundTask { Task = "AnimatedHandcuffsCompound" };
            }
            else if (HasComp<TimerTriggerComponent>(uid) || HasComp<TriggerOnUseComponent>(uid) || HasComp<ExplosiveComponent>(uid))
            {
                htn.RootTask = new HTNCompoundTask { Task = "AnimatedGrenadeCompound" };
            }
            else
            {
                htn.RootTask = new HTNCompoundTask { Task = "SimpleHostileCompound" };
            }

            htn.Blackboard.SetValue(NPCBlackboard.Owner, uid);
        }

        EnsureHelper<CanEscapeInventoryComponent>(uid, comp);

        EnsureHelper<InputMoverComponent>(uid, comp);
        EnsureHelper<MobMoverComponent>(uid, comp);
        if (EnsureHelper<MobThresholdsComponent>(uid, comp, out var thresholds))
            _thresholds.SetMobStateThreshold(uid, 30, MobState.Dead, thresholds);
    }

    private void OnComponentShutdown(EntityUid uid, RevenantAnimatedComponent comp, ComponentShutdown args)
    {
        if (comp.LifeStage != ComponentLifeStage.Stopping)
            return;

        foreach (var added in comp.AddedComponents)
        {
            if (added.Deleted)
                continue;

            RemCompDeferred(uid, added);
        }

        _popup.PopupEntity(Loc.GetString("revenant-animate-item-inanimate", ("target", uid)), uid, Filter.Pvs(uid), true);
    }

    private void OnMobStateChange(EntityUid uid, RevenantAnimatedComponent comp, MobStateChangedEvent args)
    {
        if (args.NewMobState == MobState.Dead)
            InanimateTarget(uid, comp);
    }

    private bool EnsureHelper<T>(EntityUid uid, RevenantAnimatedComponent comp, out T added) where T : Component, new()
    {
        if (TryComp<T>(uid, out var existing))
        {
            added = existing;
            return false;
        }

        added = AddComp<T>(uid);
        comp.AddedComponents.Add(added);
        return true;
    }

    private bool EnsureHelper<T>(EntityUid uid, RevenantAnimatedComponent comp) where T : Component, new()
    {
        return EnsureHelper<T>(uid, comp, out _);
    }

    public bool CanAnimateObject(EntityUid target)
    {
        return !HasComp<RevenantAnimatedComponent>(target)
               && !HasComp<MobStateComponent>(target)
               && (!TryComp(target, out TransformComponent? xform) || !xform.Anchored)
               && !HasComp<MachineComponent>(target);
    }

    public bool TryAnimateObject(EntityUid target, TimeSpan? duration = null, Entity<RevenantComponent>? revenant = null)
    {
        if (!CanAnimateObject(target))
            return false;

        var animate = EnsureComp<RevenantAnimatedComponent>(target);
        animate.Revenant = revenant?.Owner;
        if (duration != null)
            animate.EndTime = _gameTiming.CurTime + duration.Value;
        else if (revenant != null)
            animate.EndTime = _gameTiming.CurTime + revenant.Value.Comp.AnimateTime;

        return true;
    }

    public void InanimateTarget(EntityUid target, RevenantAnimatedComponent? comp = null)
    {
        if (!target.Valid || !Resolve(target, ref comp))
            return;

        RemComp<RevenantAnimatedComponent>(target);
    }
}
