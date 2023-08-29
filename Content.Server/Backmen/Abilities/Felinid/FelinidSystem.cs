﻿using Content.Shared.Actions;
using Content.Shared.Audio;
using Content.Shared.StatusEffect;
using Content.Shared.Throwing;
using Content.Shared.Item;
using Content.Shared.Inventory;
using Content.Shared.Hands;
using Content.Shared.Actions.ActionTypes;
using Content.Shared.IdentityManagement;
using Content.Shared.Nutrition.Components;
using Content.Shared.Nutrition.EntitySystems;
using Content.Server.Body.Components;
using Content.Server.Medical;
using Content.Server.Nutrition.EntitySystems;
using Content.Server.Nutrition.Components;
using Content.Server.Chemistry.EntitySystems;
using Content.Server.Popups;
using Robust.Shared.Audio;
using Robust.Shared.Player;
using Robust.Shared.Random;
using Robust.Shared.Prototypes;

namespace Content.Server.Backmen.Abilities.Felinid;

public sealed class FelinidSystem : EntitySystem
{

    [Dependency] private readonly SharedActionsSystem _actionsSystem = default!;
    [Dependency] private readonly HungerSystem _hungerSystem = default!;
    [Dependency] private readonly VomitSystem _vomitSystem = default!;
    [Dependency] private readonly SolutionContainerSystem _solutionSystem = default!;
    [Dependency] private readonly IRobustRandom _robustRandom = default!;
    [Dependency] private readonly PopupSystem _popupSystem = default!;
    [Dependency] private readonly InventorySystem _inventorySystem = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FelinidComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<FelinidComponent, HairballActionEvent>(OnHairball);
        SubscribeLocalEvent<FelinidComponent, EatMouseActionEvent>(OnEatMouse);
        SubscribeLocalEvent<FelinidComponent, DidEquipHandEvent>(OnEquipped);
        SubscribeLocalEvent<FelinidComponent,DidUnequipHandEvent>(OnUnequipped);
        SubscribeLocalEvent<HairballComponent, ThrowDoHitEvent>(OnHairballHit);
        SubscribeLocalEvent<HairballComponent, GettingPickedUpAttemptEvent>(OnHairballPickupAttempt);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        foreach (var (hairballComp, catComp) in EntityQuery<CoughingUpHairballComponent, FelinidComponent>())
        {
            hairballComp.Accumulator += frameTime;
            if (hairballComp.Accumulator < hairballComp.CoughUpTime.TotalSeconds)
                continue;

            hairballComp.Accumulator = 0;
            SpawnHairball(hairballComp.Owner, catComp);
            RemCompDeferred<CoughingUpHairballComponent>(hairballComp.Owner);
        }
    }

    private void OnInit(EntityUid uid, FelinidComponent component, ComponentInit args)
    {
        if (!_prototypeManager.TryIndex<InstantActionPrototype>("HairballAction", out var hairball))
            return;

        component.HairballAction = new InstantAction(hairball);
        _actionsSystem.AddAction(uid, component.HairballAction, uid);
    }

    private void OnEquipped(EntityUid uid, FelinidComponent component, DidEquipHandEvent args)
    {
        if (!HasComp<FelinidFoodComponent>(args.Equipped))
            return;

        component.PotentialTarget = args.Equipped;

        if (_prototypeManager.TryIndex<InstantActionPrototype>("EatMouse", out var eatMouse))
            _actionsSystem.AddAction(uid, new InstantAction(eatMouse), null);
    }

    private void OnUnequipped(EntityUid uid, FelinidComponent component, DidUnequipHandEvent args)
    {
        if (args.Unequipped == component.PotentialTarget)
        {
            component.PotentialTarget = null;
            if (_prototypeManager.TryIndex<InstantActionPrototype>("EatMouse", out var eatMouse))
                _actionsSystem.RemoveAction(uid, eatMouse);
        }
    }

    private void OnHairball(EntityUid uid, FelinidComponent component, HairballActionEvent args)
    {
        if (_inventorySystem.TryGetSlotEntity(uid, "mask", out var maskUid) &&
            EntityManager.TryGetComponent<IngestionBlockerComponent>(maskUid, out var blocker) &&
            blocker.Enabled)
        {
            _popupSystem.PopupEntity(Loc.GetString("hairball-mask", ("mask", maskUid)), uid, uid);
            return;
        }

        _popupSystem.PopupEntity(Loc.GetString("hairball-cough", ("name", Identity.Entity(uid, EntityManager))), uid);
        SoundSystem.Play("/Audio/Backmen/Effects/Species/hairball.ogg", Filter.Pvs(uid), uid, AudioHelpers.WithVariation(0.15f));

        EnsureComp<CoughingUpHairballComponent>(uid);
        args.Handled = true;
    }

    private void OnEatMouse(EntityUid uid, FelinidComponent component, EatMouseActionEvent args)
    {
        if (component.PotentialTarget == null)
            return;

        if (!TryComp<HungerComponent>(uid, out var hunger))
            return;

        if (hunger.CurrentThreshold == Shared.Nutrition.Components.HungerThreshold.Overfed)
        {
            _popupSystem.PopupEntity(Loc.GetString("food-system-you-cannot-eat-any-more"), uid, uid, Shared.Popups.PopupType.SmallCaution);
            return;
        }

        if (_inventorySystem.TryGetSlotEntity(uid, "mask", out var maskUid) &&
            EntityManager.TryGetComponent<IngestionBlockerComponent>(maskUid, out var blocker) &&
            blocker.Enabled)
        {
            _popupSystem.PopupEntity(Loc.GetString("hairball-mask", ("mask", maskUid)), uid, uid, Shared.Popups.PopupType.SmallCaution);
            return;
        }

        if (component.HairballAction != null)
        {
            _actionsSystem.SetCharges(component.HairballAction, component.HairballAction.Charges + 1);
            _actionsSystem.SetEnabled(component.HairballAction, true);
        }
        Del(component.PotentialTarget.Value);
        component.PotentialTarget = null;

        SoundSystem.Play("/Audio/Items/eatfood.ogg", Filter.Pvs(uid), uid, AudioHelpers.WithVariation(0.15f));

        _hungerSystem.ModifyHunger(uid, 70f, hunger);

        if (_prototypeManager.TryIndex<InstantActionPrototype>("EatMouse", out var eatMouse))
            _actionsSystem.RemoveAction(uid, eatMouse);
    }

    private void SpawnHairball(EntityUid uid, FelinidComponent component)
    {
        var hairball = EntityManager.SpawnEntity(component.HairballPrototype, Transform(uid).Coordinates);
        var hairballComp = Comp<HairballComponent>(hairball);

        if (TryComp<BloodstreamComponent>(uid, out var bloodstream))
        {
            var temp = bloodstream.ChemicalSolution.SplitSolution(20);

            if (_solutionSystem.TryGetSolution(hairball, hairballComp.SolutionName, out var hairballSolution))
            {
                _solutionSystem.TryAddSolution(hairball, hairballSolution, temp);
            }
        }
    }
    private void OnHairballHit(EntityUid uid, HairballComponent component, ThrowDoHitEvent args)
    {
        if (HasComp<FelinidComponent>(args.Target) || !HasComp<StatusEffectsComponent>(args.Target))
            return;
        if (_robustRandom.Prob(0.2f))
            _vomitSystem.Vomit(args.Target);
    }

    private void OnHairballPickupAttempt(EntityUid uid, HairballComponent component, GettingPickedUpAttemptEvent args)
    {
        if (HasComp<FelinidComponent>(args.User) || !HasComp<StatusEffectsComponent>(args.User))
            return;

        if (_robustRandom.Prob(0.2f))
        {
            _vomitSystem.Vomit(args.User);
            args.Cancel();
        }
    }
}

public sealed partial class HairballActionEvent : InstantActionEvent {}
public sealed partial class EatMouseActionEvent : InstantActionEvent {}
