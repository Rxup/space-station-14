﻿using System.Linq;
using System.Numerics;
using Content.Server.Chemistry.Containers.EntitySystems;
using Content.Server.Construction.Components;
using Content.Server.Coordinates.Helpers;
using Content.Server.Cuffs;
using Content.Server.Salvage.Expeditions;
using Content.Server.Station.Components;
using Content.Server.Station.Systems;
using Content.Server.Warps;
using Content.Shared.Chemistry.Components;
using Content.Shared.Cuffs.Components;
using Content.Shared.Backmen.Flesh;
using Content.Shared.Cargo.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory;
using Content.Shared.Maps;
using Content.Shared.Mobs.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Content.Shared.SubFloor;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Components;
using Robust.Shared.Player;

namespace Content.Server.Backmen.Flesh;

public sealed partial class FleshCultistSystem
{
    [Dependency] private readonly SharedAudioSystem _audioSystem = default!;
    [Dependency] private readonly SharedHandsSystem _handsSystem = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly CuffableSystem _cuffable = default!;
    [Dependency] private readonly MapSystem _map = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solutionContainerSystem = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _movement = default!;
    [Dependency] private readonly StationSystem _stationSystem = default!;
    [Dependency] private readonly SharedTransformSystem _sharedTransform = default!;
    [Dependency] private readonly EntityLookupSystem _lookupSystem = default!;

    private void InitializeAbilities()
    {
        SubscribeLocalEvent<FleshCultistComponent, FleshCultistBladeActionEvent>(OnBladeActionEvent);
        SubscribeLocalEvent<FleshCultistComponent, FleshCultistClawActionEvent>(OnClawActionEvent);
        SubscribeLocalEvent<FleshCultistComponent, FleshCultistFistActionEvent>(OnFistActionEvent);
        SubscribeLocalEvent<FleshCultistComponent, FleshCultistSpikeHandGunActionEvent>(OnSpikeHandGunActionEvent);
        SubscribeLocalEvent<FleshCultistComponent, FleshCultistArmorActionEvent>(OnArmorActionEvent);
        SubscribeLocalEvent<FleshCultistComponent, FleshCultistSpiderLegsActionEvent>(OnSpiderLegsActionEvent);
        SubscribeLocalEvent<FleshCultistComponent, FleshCultistBreakCuffsActionEvent>(OnBreakCuffsActionEvent);
        SubscribeLocalEvent<FleshCultistComponent, FleshCultistAdrenalinActionEvent>(OnAdrenalinActionEvent);
        SubscribeLocalEvent<FleshCultistComponent, FleshCultistCreateFleshHeartActionEvent>(OnCreateFleshHeartActionEvent);
        SubscribeLocalEvent<FleshCultistComponent, FleshCultistThrowWormActionEvent>(OnThrowWorm);
        SubscribeLocalEvent<FleshCultistComponent, FleshCultistAcidSpitActionEvent>(OnAcidSpit);
    }

    private void OnAcidSpit(EntityUid uid, FleshCultistComponent component, FleshCultistAcidSpitActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        var xform = Transform(uid);
        var acidBullet = Spawn(component.BulletAcidSpawnId, xform.Coordinates);

        var mapCoords = _sharedTransform.ToMapCoordinates(args.Target);
        var direction = mapCoords.Position - _sharedTransform.GetMapCoordinates(xform).Position;
        var userVelocity = _physics.GetMapLinearVelocity(uid);

        _gunSystem.ShootProjectile(acidBullet, direction, userVelocity, uid, uid);
        _audioSystem.PlayPvs(component.SoundMutation, uid, component.SoundMutation.Params);
    }

    private void OnBladeActionEvent(EntityUid uid, FleshCultistComponent component, FleshCultistBladeActionEvent args)
    {
        if (args.Handled)
            return;
        var hands = _handsSystem.EnumerateHands(uid);
        var enumerateHands = hands as Hand[] ?? hands.ToArray();
        foreach (var enumerateHand in enumerateHands)
        {
            if (enumerateHand.Container == null)
                continue;
            foreach (var containerContainedEntity in enumerateHand.Container.ContainedEntities)
            {
                if (!TryComp(containerContainedEntity, out MetaDataComponent? metaData))
                    continue;
                if (metaData.EntityPrototype == null)
                    continue;
                if (!(metaData.EntityPrototype.ID == component.BladeSpawnId ||
                      metaData.EntityPrototype.ID == component.ClawSpawnId ||
                      metaData.EntityPrototype.ID == component.SpikeHandGunSpawnId ||
                      metaData.EntityPrototype.ID == component.FistSpawnId))
                {
                    if (enumerateHand != enumerateHands.First())
                        continue;
                    var isDrop = _handsSystem.TryDrop(uid, checkActionBlocker: false);
                    if (metaData.EntityPrototype.ID == component.BladeSpawnId)
                        continue;
                    if (isDrop)
                        continue;
                    _popup.PopupEntity(Loc.GetString("flesh-cultist-transform-user-hand-blocked"),
                        uid, uid, PopupType.Large);
                    return;
                }
                {
                    if (enumerateHand != enumerateHands.First())
                    {
                        if (metaData.EntityPrototype.ID != component.BladeSpawnId)
                            continue;
                        QueueDel(containerContainedEntity);
                    }
                    else
                    {
                        if (metaData.EntityPrototype.ID != component.BladeSpawnId)
                        {
                            _popup.PopupEntity(Loc.GetString("flesh-cultist-transform-user-hand-blocked"),
                                uid, uid, PopupType.Large);
                        }
                        else
                        {
                            QueueDel(containerContainedEntity);
                            _popup.PopupEntity(Loc.GetString("flesh-cultist-transform-blade-in-hand",
                                ("Entity", uid)), uid, PopupType.LargeCaution);
                            _audioSystem.PlayPvs(component.SoundMutation, uid, component.SoundMutation.Params);
                            EnsureComp<CuffableComponent>(uid);
                            args.Handled = true;
                        }

                        return;
                    }
                }
            }
        }

        var blade = Spawn(component.BladeSpawnId, Transform(uid).Coordinates);
        var isPickup = _handsSystem.TryPickup(uid, blade, checkActionBlocker: false,
            animateUser: false, animate: false);
        if (isPickup)
        {
            _audioSystem.PlayPvs(component.SoundMutation, uid, component.SoundMutation.Params);
            _popup.PopupEntity(Loc.GetString("flesh-cultist-transform-hand-in-blade", ("Entity", uid)),
                uid, PopupType.LargeCaution);
            if (TryComp<CuffableComponent>(uid, out var cuffableComponent))
            {
                RemComp<CuffableComponent>(uid);
            }
        }
        else
        {
            Log.Error("Failed to equip blade to hand, removing blade");
            QueueDel(blade);
        }
        args.Handled = true;
    }

    private void OnClawActionEvent(EntityUid uid, FleshCultistComponent component, FleshCultistClawActionEvent args)
    {
        if (args.Handled)
            return;

        var hands = _handsSystem.EnumerateHands(uid);
        var enumerateHands = hands as Hand[] ?? hands.ToArray();
        foreach (var enumerateHand in enumerateHands)
        {
            if (enumerateHand.Container == null)
                continue;
            foreach (var containerContainedEntity in enumerateHand.Container.ContainedEntities)
            {
                if (!TryComp(containerContainedEntity, out MetaDataComponent? metaData))
                    continue;
                if (metaData.EntityPrototype == null)
                    continue;
                if (!(metaData.EntityPrototype.ID == component.BladeSpawnId ||
                      metaData.EntityPrototype.ID == component.ClawSpawnId ||
                      metaData.EntityPrototype.ID == component.SpikeHandGunSpawnId ||
                      metaData.EntityPrototype.ID == component.FistSpawnId))
                {
                    if (enumerateHand != enumerateHands.First())
                        continue;
                    var isDrop = _handsSystem.TryDrop(uid, checkActionBlocker: false);
                    if (metaData.EntityPrototype.ID == component.ClawSpawnId)
                        continue;
                    if (isDrop)
                        continue;
                    _popup.PopupEntity(Loc.GetString("flesh-cultist-transform-user-hand-blocked"),
                        uid, uid, PopupType.Large);
                    return;
                }
                {
                    if (enumerateHand != enumerateHands.First())
                    {
                        if (metaData.EntityPrototype.ID != component.ClawSpawnId)
                            continue;
                        QueueDel(containerContainedEntity);
                    }
                    else
                    {
                        if (metaData.EntityPrototype.ID != component.ClawSpawnId)
                        {
                            _popup.PopupEntity(Loc.GetString("flesh-cultist-transform-user-hand-blocked"),
                                uid, uid, PopupType.Large);
                        }
                        else
                        {
                            QueueDel(containerContainedEntity);
                            _popup.PopupEntity(Loc.GetString("flesh-cultist-transform-claw-in-hand",
                                ("Entity", uid)), uid, PopupType.LargeCaution);
                            _audioSystem.PlayPvs(component.SoundMutation, uid, component.SoundMutation.Params);
                            EnsureComp<CuffableComponent>(uid);
                            args.Handled = true;
                        }
                        return;
                    }
                }
            }
        }

        var claw = Spawn(component.ClawSpawnId, Transform(uid).Coordinates);
        var isPickup = _handsSystem.TryPickup(uid, claw, checkActionBlocker: false,
            animateUser: false, animate: false);
        if (isPickup)
        {
            _audioSystem.PlayPvs(component.SoundMutation, uid, component.SoundMutation.Params);
            _popup.PopupEntity(Loc.GetString("flesh-cultist-transform-hand-in-claw", ("Entity", uid)),
                uid, PopupType.LargeCaution);
            if (TryComp<CuffableComponent>(uid, out var cuffableComponent))
            {
                RemComp<CuffableComponent>(uid);
            }
        }
        else
        {
            QueueDel(claw);
        }
        args.Handled = true;
    }


    private void OnFistActionEvent(EntityUid uid, FleshCultistComponent component, FleshCultistFistActionEvent args)
    {
        if (args.Handled)
            return;

        var hands = _handsSystem.EnumerateHands(uid);
        var enumerateHands = hands as Hand[] ?? hands.ToArray();
        foreach (var enumerateHand in enumerateHands)
        {
            if (enumerateHand.Container == null)
                continue;
            foreach (var containerContainedEntity in enumerateHand.Container.ContainedEntities)
            {
                if (!TryComp(containerContainedEntity, out MetaDataComponent? metaData))
                    continue;
                if (metaData.EntityPrototype == null)
                    continue;
                if (!(metaData.EntityPrototype.ID == component.BladeSpawnId ||
                      metaData.EntityPrototype.ID == component.ClawSpawnId ||
                      metaData.EntityPrototype.ID == component.SpikeHandGunSpawnId ||
                      metaData.EntityPrototype.ID == component.FistSpawnId))
                {
                    if (enumerateHand != enumerateHands.First())
                        continue;
                    var isDrop = _handsSystem.TryDrop(uid, checkActionBlocker: false);
                    if (metaData.EntityPrototype.ID == component.FistSpawnId)
                        continue;
                    if (isDrop)
                        continue;
                    _popup.PopupEntity(Loc.GetString("flesh-cultist-transform-user-hand-blocked"),
                        uid, uid, PopupType.Large);
                    return;
                }
                {
                    if (enumerateHand != enumerateHands.First())
                    {
                        if (metaData.EntityPrototype.ID != component.FistSpawnId)
                            continue;
                        QueueDel(containerContainedEntity);
                    }
                    else
                    {
                        if (metaData.EntityPrototype.ID != component.FistSpawnId)
                        {
                            _popup.PopupEntity(Loc.GetString("flesh-cultist-transform-user-hand-blocked"),
                                uid, uid, PopupType.Large);
                        }
                        else
                        {
                            QueueDel(containerContainedEntity);
                            _popup.PopupEntity(Loc.GetString("flesh-cultist-transform-claw-in-hand",
                                ("Entity", uid)), uid, PopupType.LargeCaution);
                            _audioSystem.PlayPvs(component.SoundMutation, uid, component.SoundMutation.Params);
                            EnsureComp<CuffableComponent>(uid);
                            args.Handled = true;
                        }
                        return;
                    }
                }
            }
        }

        var fist = Spawn(component.FistSpawnId, Transform(uid).Coordinates);
        var isPickup = _handsSystem.TryPickup(uid, fist, checkActionBlocker: false,
            animateUser: false, animate: false);
        if (isPickup)
        {
            _audioSystem.PlayPvs(component.SoundMutation, uid, component.SoundMutation.Params);
            _popup.PopupEntity(Loc.GetString("flesh-cultist-transform-hand-in-claw", ("Entity", uid)),
                uid, PopupType.LargeCaution);
            if (TryComp<CuffableComponent>(uid, out var cuffableComponent))
            {
                EntityManager.RemoveComponent<CuffableComponent>(uid);
            }
        }
        else
        {
            QueueDel(fist);
        }
        args.Handled = true;
    }

    private void OnSpikeHandGunActionEvent(EntityUid uid, FleshCultistComponent component, FleshCultistSpikeHandGunActionEvent args)
    {
        if (args.Handled)
            return;

        var hands = _handsSystem.EnumerateHands(uid);
        var enumerateHands = hands as Hand[] ?? hands.ToArray();
        foreach (var enumerateHand in enumerateHands)
        {
            if (enumerateHand.Container == null)
                continue;
            foreach (var containerContainedEntity in enumerateHand.Container.ContainedEntities)
            {
                if (!TryComp(containerContainedEntity, out MetaDataComponent? metaData))
                    continue;
                if (metaData.EntityPrototype == null)
                    continue;
                if (!(metaData.EntityPrototype.ID == component.BladeSpawnId ||
                      metaData.EntityPrototype.ID == component.ClawSpawnId ||
                      metaData.EntityPrototype.ID == component.SpikeHandGunSpawnId ||
                      metaData.EntityPrototype.ID == component.FistSpawnId))
                {
                    if (enumerateHand != enumerateHands.First())
                        continue;
                    var isDrop = _handsSystem.TryDrop(uid, checkActionBlocker: false);
                    if (metaData.EntityPrototype.ID == component.SpikeHandGunSpawnId)
                        continue;
                    if (isDrop)
                        continue;
                    _popup.PopupEntity(Loc.GetString("flesh-cultist-transform-user-hand-blocked"),
                        uid, uid, PopupType.Large);
                    return;
                }
                {
                    if (enumerateHand != enumerateHands.First())
                    {
                        if (metaData.EntityPrototype.ID != component.SpikeHandGunSpawnId)
                            continue;
                        QueueDel(containerContainedEntity);
                    }
                    else
                    {
                        if (metaData.EntityPrototype.ID != component.SpikeHandGunSpawnId)
                        {
                            _popup.PopupEntity(Loc.GetString("flesh-cultist-transform-user-hand-blocked"),
                                uid, uid, PopupType.Large);
                        }
                        else
                        {
                            QueueDel(containerContainedEntity);
                            _popup.PopupEntity(Loc.GetString("flesh-cultist-transform-spike-gun-in-hand",
                                ("Entity", uid)), uid, PopupType.LargeCaution);
                            _audioSystem.PlayPvs(component.SoundMutation, uid, component.SoundMutation.Params);
                            EnsureComp<CuffableComponent>(uid);
                            args.Handled = true;
                        }
                        return;
                    }
                }
            }
        }

        var claw = Spawn(component.SpikeHandGunSpawnId, Transform(uid).Coordinates);
        var isPickup = _handsSystem.TryPickup(uid, claw, checkActionBlocker: false,
            animateUser: false, animate: false);
        if (isPickup)
        {
            _audioSystem.PlayPvs(component.SoundMutation, uid, component.SoundMutation.Params);
            _popup.PopupEntity(Loc.GetString("flesh-cultist-transform-hand-in-spike-gun", ("Entity", uid)),
                uid, PopupType.LargeCaution);
            if (TryComp<CuffableComponent>(uid, out var cuffableComponent))
            {
                RemComp<CuffableComponent>(uid);
            }
        }
        else
        {
            QueueDel(claw);
        }
        args.Handled = true;
    }

    private void OnArmorActionEvent(EntityUid uid, FleshCultistComponent component, FleshCultistArmorActionEvent args)
    {
        _inventory.TryGetSlotEntity(uid, "shoes", out var shoes);
        _inventory.TryGetSlotEntity(uid, "outerClothing", out var outerClothing);
        if (shoes != null)
        {
            if (!TryComp(shoes, out MetaDataComponent? metaData))
                return;
            if (metaData.EntityPrototype == null)
                return;
            if (metaData.EntityPrototype.ID == component.SpiderLegsSpawnId)
            {
                _popup.PopupEntity(Loc.GetString("flesh-cultist-transform-armor-blocked"),
                    uid, uid, PopupType.Large);
                return;
            }
        }
        if (outerClothing != null)
        {
            if (!TryComp(outerClothing, out MetaDataComponent? metaData))
                return;
            if (metaData.EntityPrototype == null)
                return;
            if (metaData.EntityPrototype.ID != component.ArmorSpawnId)
            {
                _inventory.TryUnequip(uid, "outerClothing", true, true);
                var armor = Spawn(component.ArmorSpawnId, Transform(uid).Coordinates);
                var equipped = _inventory.TryEquip(uid, armor, "outerClothing", true);
                if (!equipped)
                {
                    QueueDel(armor);
                }
                else
                {
                    _audioSystem.PlayPvs(component.SoundMutation, uid, component.SoundMutation.Params);
                    _popup.PopupEntity(Loc.GetString("flesh-cultist-transform-armor-on",
                        ("Entity", uid)), uid, PopupType.LargeCaution);
                    args.Handled = true;
                }
            }
            else
            {
                _audioSystem.PlayPvs(component.SoundMutation, uid, component.SoundMutation.Params);
                _popup.PopupEntity(Loc.GetString("flesh-cultist-transform-armor-off",
                    ("Entity", uid)), uid, PopupType.LargeCaution);
                EntityManager.DeleteEntity(outerClothing.Value);
                _movement.RefreshMovementSpeedModifiers(uid);
                args.Handled = true;
            }
        }
        else
        {
            var armor = Spawn(component.ArmorSpawnId, Transform(uid).Coordinates);
            var equipped = _inventory.TryEquip(uid, armor, "outerClothing", true);
            if (!equipped)
            {
                QueueDel(armor);
            }
            else
            {
                _audioSystem.PlayPvs(component.SoundMutation, uid, component.SoundMutation.Params);
                _popup.PopupEntity(Loc.GetString("flesh-cultist-transform-armor-on",
                        ("Entity", uid)), uid, PopupType.LargeCaution);
                args.Handled = true;
            }
        }
    }

    private void OnSpiderLegsActionEvent(EntityUid uid, FleshCultistComponent component, FleshCultistSpiderLegsActionEvent args)
    {
        _inventory.TryGetSlotEntity(uid, "shoes", out var shoes);
        _inventory.TryGetSlotEntity(uid, "outerClothing", out var outerClothing);
        if (outerClothing != null)
        {
            if (!TryComp(shoes, out MetaDataComponent? metaData))
                return;
            if (metaData.EntityPrototype == null)
                return;
            if (metaData.EntityPrototype.ID == component.ArmorSpawnId)
            {
                _popup.PopupEntity(Loc.GetString("flesh-cultist-transform-spider-legs-blocked"),
                    uid, uid, PopupType.Large);
                return;
            }
        }
        if (shoes != null)
        {
            if (!TryComp(shoes, out MetaDataComponent? metaData))
                return;
            if (metaData.EntityPrototype == null)
                return;
            if (metaData.EntityPrototype.ID != component.SpiderLegsSpawnId)
            {
                _inventory.TryUnequip(uid, "outerClothing", true, true);
                _inventory.TryUnequip(uid, "shoes", true, true);
                var armor = Spawn(component.SpiderLegsSpawnId, Transform(uid).Coordinates);
                var equipped = _inventory.TryEquip(uid, armor, "shoes", true);
                if (!equipped)
                {
                    QueueDel(armor);
                }
                else
                {
                    _audioSystem.PlayPvs(component.SoundMutation, uid, component.SoundMutation.Params);
                    _popup.PopupEntity(Loc.GetString("flesh-cultist-transform-spider-legs-on",
                        ("Entity", uid)), uid, PopupType.LargeCaution);
                    args.Handled = true;
                }
            }
            else
            {
                _audioSystem.PlayPvs(component.SoundMutation, uid, component.SoundMutation.Params);
                _popup.PopupEntity(Loc.GetString("flesh-cultist-transform-spider-legs-off",
                    ("Entity", uid)), uid, PopupType.LargeCaution);
                EntityManager.DeleteEntity(shoes.Value);
                _movement.RefreshMovementSpeedModifiers(uid);
                args.Handled = true;
            }
        }
        else
        {
            _inventory.TryUnequip(uid, "outerClothing", true, true);
            var spiderLegs = Spawn(component.SpiderLegsSpawnId, Transform(uid).Coordinates);
            var equipped = _inventory.TryEquip(uid, spiderLegs, "shoes", true);
            if (!equipped)
            {
                QueueDel(spiderLegs);
            }
            else
            {
                _audioSystem.PlayPvs(component.SoundMutation, uid, component.SoundMutation.Params);
                _popup.PopupEntity(Loc.GetString("flesh-cultist-transform-spider-legs-on",
                    ("Entity", uid)), uid, PopupType.LargeCaution);
                args.Handled = true;
            }
        }
    }

    private void OnBreakCuffsActionEvent(EntityUid uid, FleshCultistComponent component, FleshCultistBreakCuffsActionEvent args)
    {
        if (!TryComp<CuffableComponent>(uid, out var cuffs) || cuffs.Container.ContainedEntities.Count < 1)
            return;

        _cuffable.Uncuff(uid, cuffs.LastAddedCuffs, cuffs.LastAddedCuffs);
        args.Handled = true;
    }

    private void OnAdrenalinActionEvent(EntityUid uid, FleshCultistComponent component, FleshCultistAdrenalinActionEvent args)
    {
        if (!_solutionContainerSystem.TryGetInjectableSolution(uid, out var soln, out var injectableSolution))
            return;
        var transferSolution = new Solution();
        foreach (var reagent in component.AdrenalinReagents)
        {
            transferSolution.AddReagent(reagent.Reagent, reagent.Quantity);
        }
        _solutionContainerSystem.TryAddSolution(soln.Value, transferSolution);
        args.Handled = true;
    }

    private void OnCreateFleshHeartActionEvent(EntityUid uid, FleshCultistComponent component, FleshCultistCreateFleshHeartActionEvent args)
    {
        var xform = Transform(uid);
        var radius = 1.5f;
        if (!TryComp<MapGridComponent>(xform.GridUid, out var grid))
        {
            _popup.PopupEntity(Loc.GetString("flesh-cultist-cant-spawn-flesh-heart",
                ("Entity", uid)),
                uid,
                PopupType.Large);
            return;
        }

        var station = _stationSystem.GetOwningStation(xform.GridUid.Value);
        var isCargo = HasComp<CargoShuttleComponent>(xform.GridUid.Value) ||
                      HasComp<SalvageShuttleComponent>(xform.GridUid.Value);
        if (station == null || !HasComp<StationEventEligibleComponent>(station) || isCargo || !HasComp<BecomesStationComponent>(xform.GridUid.Value))
        {
            _popup.PopupEntity(Loc.GetString("flesh-cultist-cant-spawn-flesh-heart",
                ("Entity", uid)),
                uid,
                PopupType.Large);
            return;
        }

        var offsetValue = xform.LocalRotation.ToWorldVec();
        var targetCord = xform.Coordinates.Offset(offsetValue).SnapToGrid(EntityManager);
        var tilerefs = new Box2(targetCord.Position + new Vector2(-radius, -radius), targetCord.Position + new Vector2(radius, radius));

        foreach (var entity in _lookupSystem.GetEntitiesIntersecting(xform.GridUid.Value, tilerefs, LookupFlags.Uncontained))
        {
            if(entity == uid)
                continue;

            if(HasComp<SubFloorHideComponent>(entity))
                continue;

            if(HasComp<WarpPointComponent>(entity))
                continue;

            if (HasComp<MobStateComponent>(entity) || // Is it a mob?
                !TryComp<PhysicsComponent>(entity, out var physics) || (physics.CollisionLayer & (int) CollisionGroup.Impassable) != 0 ||
                HasComp<ConstructionComponent>(entity)) // Is construction?
            {
                _popup.PopupEntity(Loc.GetString("flesh-cultist-cant-spawn-flesh-heart-here",
                    ("Entity", entity)),
                    uid,
                    PopupType.Large);
                return;
            }
        }
        _audioSystem.PlayPvs(component.SoundMutation, uid, component.SoundMutation.Params);
        Spawn(component.FleshHeartId, targetCord);
        args.Handled = true;
    }

    private void OnThrowWorm(EntityUid uid, FleshCultistComponent component, FleshCultistThrowWormActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        var worm = Spawn(component.WormMobSpawnId, Transform(uid).Coordinates);
        _handsSystem.TryPickup(uid, worm, checkActionBlocker: false, animateUser: false, animate: false);
        if (component.SoundThrowWorm != null)
        {
            _audioSystem.PlayPvs(component.SoundThrowWorm, uid, component.SoundThrowWorm.Params);
        }
        _popup.PopupEntity(Loc.GetString("flesh-cultist-throw-worm"), uid, uid,
            PopupType.LargeCaution);
        _popup.PopupEntity(Loc.GetString("flesh-cultist-throw-worm-others", ("Entity", uid)),
            uid, Filter.PvsExcept(uid), true, PopupType.LargeCaution);
    }

}
