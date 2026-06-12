using System.Linq;
using Content.Server.Actions;
using Content.Server.Backmen.Surgery.Trauma.Systems;
using Content.Server.Backmen.Surgery.Wounds.Systems;
using Content.Server.Hands.Systems;
using Content.Server.NPC.Components;
using Content.Server.NPC.Systems;
using Content.Server.Popups;
using Content.Shared.Backmen.Damage;
using Content.Shared.Backmen.Flesh;
using Content.Shared.Backmen.Surgery.Traumas;
using Content.Shared.Backmen.Surgery.Traumas.Components;
using Content.Shared.Backmen.Surgery.Wounds.Components;
using Content.Shared.Backmen.Targeting;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Clothing.Components;
using Content.Shared.Clothing.EntitySystems;
using Content.Shared.CombatMode;
using Content.Shared.CombatMode.Pacification;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Hands;
using Content.Shared.Humanoid;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Nutrition;
using Content.Shared.Nutrition.Components;
using Content.Shared.Popups;
using Content.Shared.Stunnable;
using Content.Shared.Throwing;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.Backmen.Flesh;

public sealed partial class FleshWormSystem : SharedFleshWormSystem
{
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedStunSystem _stunSystem = default!;
    [Dependency] private DamageableSystem _damageableSystem = default!;
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private SharedCombatModeSystem _combat = default!;
    [Dependency] private ThrowingSystem _throwing = default!;
    [Dependency] private SharedAudioSystem _audioSystem = default!;
    [Dependency] private ActionsSystem _action = default!;
    [Dependency] private NPCSystem _npc = default!;
    [Dependency] private HandsSystem _hands = default!;
    [Dependency] private SharedStaminaSystem _stamina = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedBodySystem _body = default!;
    [Dependency] private ServerWoundSystem _wound = default!;
    [Dependency] private ServerTraumaSystem _trauma = default!;
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private MaskSystem _mask = default!;
    [Dependency] private ToggleableClothingSystem _toggleableClothing = default!;

    private static readonly SlotFlags FaceSlots = SlotFlags.HEAD | SlotFlags.MASK;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FleshWormComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<FleshWormComponent, MeleeHitEvent>(OnMeleeHit);
        SubscribeLocalEvent<FleshWormComponent, ThrowDoHitEvent>(OnWormDoHit);
        SubscribeLocalEvent<FleshWormComponent, GotEquippedEvent>(OnGotEquipped);
        SubscribeLocalEvent<FleshWormComponent, GotUnequippedEvent>(OnGotUnequipped);
        SubscribeLocalEvent<FleshWormComponent, GotEquippedHandEvent>(OnGotEquippedHand);
        SubscribeLocalEvent<FleshWormComponent, MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<FleshWormComponent, BeingUnequippedAttemptEvent>(OnUnequipAttempt);
        SubscribeLocalEvent<FleshWormComponent, FleshWormJumpActionEvent>(OnJumpWorm);
        SubscribeLocalEvent<FleshWormComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<FleshWormComponent, FleshWormRemoveDoAfterEvent>(OnRemoveDoAfter);
    }

    public bool CanPounce(EntityUid worm, EntityUid target, FleshWormComponent? component = null)
    {
        return CanPounceBasic(worm, target, component) && !IsFaceBlocked(target);
    }

    private bool CanPounceBasic(EntityUid worm, EntityUid target, FleshWormComponent? component = null)
    {
        if (!Resolve(worm, ref component) || component.IsDeath || component.EquipedOn.Valid)
            return false;

        if (HasComp<FleshCultistComponent>(target))
            return false;

        if (!HasComp<HumanoidAppearanceComponent>(target))
            return false;

        if (TryComp(target, out MobStateComponent? mobState) && mobState.CurrentState != MobState.Alive)
            return false;

        return true;
    }

    public bool NPCTryPounce(EntityUid worm, EntityUid target, FleshWormComponent? component = null)
    {
        if (!Resolve(worm, ref component))
            return false;

        if (component.PendingPounceTarget is { Valid: true })
        {
            target = component.PendingPounceTarget;
            component.PendingPounceTarget = EntityUid.Invalid;
        }

        return TryPounce(worm, target, rollChance: false, component);
    }

    public bool TryPounce(EntityUid worm, EntityUid target, bool rollChance = true, FleshWormComponent? component = null)
    {
        if (!CanPounceBasic(worm, target, component))
            return false;

        if (rollChance && _random.Next(1, 101) > component.ChansePounce)
            return false;

        TryClearFaceProtection(target);

        if (IsFaceBlocked(target))
            return false;

        TryClearTargetMask(worm, target);

        if (!_inventory.TryEquip(target, worm, "mask", silent: true, force: true))
            return false;

        component.EquipedOn = target;
        ApplyPounceEffects(worm, target, component);
        return true;
    }

    private bool IsFaceBlocked(EntityUid target)
    {
        var attempt = new IngestionAttemptEvent(FaceSlots);
        RaiseLocalEvent(target, ref attempt);
        return attempt.Cancelled;
    }

    private void TryClearFaceProtection(EntityUid target)
    {
        TryClearMaskBlocker(target);

        if (IsFaceBlocked(target))
            TryUnequipSlotBlocker(target, "head");
    }

    private void TryClearMaskBlocker(EntityUid target)
    {
        if (!_inventory.TryGetSlotEntity(target, "mask", out var maskUid) || maskUid is not { } mask)
            return;

        if (TryComp<MaskComponent>(mask, out var maskComp) && maskComp.IsToggleable && !maskComp.IsToggled)
        {
            _mask.SetToggled(mask, true);
            return;
        }

        TryUnequipSlotBlocker(target, "mask");
    }

    private void TryUnequipSlotBlocker(EntityUid target, string slot)
    {
        if (_toggleableClothing.TryStowAttached(target, slot))
            return;

        if (!_inventory.TryGetSlotEntity(target, slot, out var itemUid) || itemUid is not { } item)
            return;

        if (!TryComp<IngestionBlockerComponent>(item, out var blocker) || !blocker.Enabled)
            return;

        _inventory.TryUnequip(target, slot, silent: true, force: true);
    }

    private void TryClearTargetMask(EntityUid worm, EntityUid target)
    {
        if (!_inventory.TryGetSlotEntity(target, "mask", out var maskUid) || maskUid is not { } mask || mask == worm)
            return;

        _inventory.TryUnequip(target, "mask", silent: true, force: true);
    }

    private void ApplyPounceEffects(EntityUid worm, EntityUid target, FleshWormComponent component)
    {
        _popup.PopupEntity(Loc.GetString("flesh-pudge-throw-worm-hit-user"),
            target, target, PopupType.LargeCaution);

        _popup.PopupEntity(Loc.GetString("flesh-pudge-throw-worm-hit-mob", ("entity", target)),
            worm, worm, PopupType.LargeCaution);

        _popup.PopupEntity(Loc.GetString("flesh-pudge-throw-worm-eat-face-others",
            ("entity", target)), target, Filter.PvsExcept(worm), true, PopupType.Large);

        EnsureComp<PacifiedComponent>(worm);
        _stunSystem.TryUpdateParalyzeDuration(target, TimeSpan.FromSeconds(component.ParalyzeTime));
        ApplyHeadDamage(worm, target, component);
    }

    private void ApplyHeadDamage(EntityUid worm, EntityUid target, FleshWormComponent component)
    {
        _damageableSystem.TryChangeDamage(target, component.Damage, out _, ignoreResistances: true,
            interruptsDoAfters: false, origin: worm, targetPart: TargetBodyPart.Head);
        TryRollHeadTrauma(target, component);
    }

    private void TryRollHeadTrauma(EntityUid victim, FleshWormComponent component)
    {
        if (component.HeadTraumaChance <= 0 || !_random.Prob(component.HeadTraumaChance))
            return;

        var headPart = _body.GetBodyChildrenOfType(victim, BodyPartType.Head).FirstOrDefault();
        if (headPart == default || !TryComp<WoundableComponent>(headPart.Id, out var headWoundable))
            return;

        Entity<WoundComponent>? piercingWound = null;
        foreach (var wound in _wound.GetWoundableWounds(headPart.Id, headWoundable))
        {
            if (DamageSpecifierAliases.IsPiercingDamageType(wound.Comp.DamageType, _prototype))
            {
                piercingWound = wound;
                break;
            }
        }

        if (piercingWound == null
            && !_wound.TryInduceWound(headPart.Id, "Piercing", component.TraumaSeverity, out piercingWound, headWoundable))
        {
            return;
        }

        if (!TryComp<TraumaInflicterComponent>(piercingWound.Value.Owner, out var inflicterComp))
            return;

        var severity = FixedPoint2.New(component.TraumaSeverity);
        var traumaType = _random.Prob(0.5f) ? TraumaType.OrganDamage : TraumaType.BoneDamage;

        if (!_trauma.TryApplyTraumas(
                (headPart.Id, headWoundable),
                (piercingWound.Value.Owner, inflicterComp),
                [traumaType],
                severity))
        {
            return;
        }

        _popup.PopupEntity(Loc.GetString("flesh-worm-head-trauma-user"),
            victim, victim, PopupType.MediumCaution);
        _popup.PopupEntity(Loc.GetString("flesh-worm-head-trauma-others", ("entity", victim)),
            victim, Filter.PvsExcept(victim), true, PopupType.MediumCaution);
    }

    private void OnShutdown(Entity<FleshWormComponent> ent, ref ComponentShutdown args)
    {
        _action.RemoveAction(ent.Owner, ent.Comp.WormJumpAction);
    }

    private void OnStartup(EntityUid uid, FleshWormComponent component, ComponentStartup args)
    {
        _action.AddAction(uid, ref component.WormJumpAction, component.ActionWormJump);
    }

    private void OnWormDoHit(EntityUid uid, FleshWormComponent component, ThrowDoHitEvent args)
    {
        if (component.IsDeath)
            return;

        TryPounce(uid, args.Target, rollChance: false, component);
    }

    private void OnGotEquipped(EntityUid uid, FleshWormComponent component, GotEquippedEvent args)
    {
        if (args.Slot != "mask")
            return;

        component.EquipedOn = args.Equipee;
        component.PendingPounceTarget = EntityUid.Invalid;
        EnsureComp<PacifiedComponent>(uid);

        _npc.SleepNPC(uid);
    }

    private void OnUnequipAttempt(Entity<FleshWormComponent> ent, ref BeingUnequippedAttemptEvent args)
    {
        if (args.Slot != "mask" || ent.Comp.EquipedOn != args.UnEquipTarget)
            return;

        if (args.Unequipee != args.UnEquipTarget)
            return;

        args.Cancel();

        if (!HasActiveWormRemoveDoAfter(args.UnEquipTarget))
            StartRemoveDoAfter(args.UnEquipTarget, ent.Owner, args.UnEquipTarget, ent.Comp);
    }

    private void OnRemoveDoAfter(Entity<FleshWormComponent> ent, ref FleshWormRemoveDoAfterEvent args)
    {
        if (args.Cancelled || args.Args.Target is not { } wearer)
            return;

        var user = args.User;
        var isSelf = args.IsSelf;

        if (!TryComp<FleshWormComponent>(ent.Owner, out var comp) || comp.EquipedOn != wearer)
            return;

        if (isSelf)
            _stamina.TakeStaminaDamage(wearer, comp.SelfRemoveStaminaCost, with: ent.Owner);

        if (!_inventory.TryUnequip(user, wearer, "mask", force: isSelf, triggerHandContact: !isSelf))
            return;

        if (!isSelf)
            _hands.TryPickup(user, ent.Owner);
    }

    private void OnGotEquippedHand(EntityUid uid, FleshWormComponent component, GotEquippedHandEvent args)
    {
        if (HasComp<FleshPudgeComponent>(args.User))
            return;
        if (HasComp<FleshCultistComponent>(args.User))
            return;
        if (component.IsDeath)
            return;

        _damageableSystem.TryChangeDamage(args.User, component.Damage, interruptsDoAfters: false);
        _popup.PopupEntity(Loc.GetString("flesh-pudge-throw-worm-bite-user"),
            args.User, args.User);
    }

    private void OnGotUnequipped(EntityUid uid, FleshWormComponent component, GotUnequippedEvent args)
    {
        if (args.Slot != "mask")
            return;

        component.EquipedOn = EntityUid.Invalid;
        component.PendingPounceTarget = EntityUid.Invalid;
        RemCompDeferred<PacifiedComponent>(uid);
        var combatMode = EnsureComp<CombatModeComponent>(uid);
        _combat.SetInCombatMode(uid, true, combatMode);
        _npc.WakeNPC(uid);
    }

    private void OnMeleeHit(EntityUid uid, FleshWormComponent component, MeleeHitEvent args)
    {
        if (!HasComp<NPCMeleeCombatComponent>(uid))
            return;

        foreach (var entity in args.HitEntities)
        {
            if (!CanPounce(uid, entity, component))
                continue;

            if (_random.Next(1, 101) > component.ChansePounce)
                return;

            component.PendingPounceTarget = entity;
            return;
        }
    }

    private static void OnMobStateChanged(EntityUid uid, FleshWormComponent component, MobStateChangedEvent args)
    {
        if (args.NewMobState == MobState.Dead)
            component.IsDeath = true;
    }

    private void OnJumpWorm(EntityUid uid, FleshWormComponent component, FleshWormJumpActionEvent args)
    {
        if (args.Handled || component.EquipedOn.Valid)
            return;

        args.Handled = true;
        var xform = Transform(uid);
        var mapCoords = _transform.ToMapCoordinates(args.Target);
        var direction = mapCoords.Position - _transform.GetMapCoordinates(xform).Position;

        _throwing.TryThrow(uid, direction, 7F, uid, 10F);
        if (component.SoundWormJump != null)
            _audioSystem.PlayPvs(component.SoundWormJump, uid, component.SoundWormJump.Params);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<FleshWormComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            comp.Accumulator += frameTime;

            if (comp.Accumulator <= comp.DamageFrequency)
                continue;

            comp.Accumulator = 0;

            if (comp.EquipedOn is not { Valid: true } targetId)
                continue;

            if (HasComp<FleshCultistComponent>(comp.EquipedOn))
                continue;

            if (TryComp(targetId, out MobStateComponent? mobState) && mobState.CurrentState != MobState.Alive)
            {
                _inventory.TryUnequip(targetId, "mask", silent: true, force: true);
                comp.EquipedOn = EntityUid.Invalid;
                continue;
            }

            ApplyHeadDamage(uid, targetId, comp);
            _popup.PopupEntity(Loc.GetString("flesh-pudge-throw-worm-eat-face-user"),
                targetId, targetId, PopupType.LargeCaution);
            _popup.PopupEntity(Loc.GetString("flesh-pudge-throw-worm-eat-face-others",
                ("entity", targetId)), targetId, Filter.PvsExcept(targetId), true);
        }
    }
}
