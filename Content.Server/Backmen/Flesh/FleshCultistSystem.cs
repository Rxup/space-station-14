using System.Linq;
using Content.Server.Actions;
using Content.Server.Atmos.Components;
using Content.Server.Body.Components;
using Content.Server.Body.Systems;
using Content.Server.Flash.Components;
using Content.Server.Fluids.EntitySystems;
using Content.Server.Forensics;
using Content.Server.Humanoid;
using Content.Server.Mind;
using Content.Server.Popups;
using Content.Server.Store.Components;
using Content.Server.Store.Systems;
using Content.Server.Temperature.Components;
using Content.Server.Weapons.Ranged.Systems;
using Content.Shared.Alert;
using Content.Shared.Body.Components;
using Content.Shared.Body.Part;
using Content.Shared.Chemistry.Components;
using Content.Shared.Cuffs.Components;
using Content.Shared.Damage;
using Content.Shared.DoAfter;
using Content.Shared.Electrocution;
using Content.Shared.FixedPoint;
using Content.Shared.Backmen.Flesh;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Cloning;
using Content.Shared.Fluids.Components;
using Content.Shared.Hands.Components;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Interaction.Components;
using Content.Shared.Inventory.Events;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Content.Shared.Random;
using Content.Shared.Tag;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Collections;
using Robust.Shared.Containers;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server.Backmen.Flesh;

public sealed partial class FleshCultistSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly ActionsSystem _action = default!;
    [Dependency] private readonly AlertsSystem _alerts = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly StoreSystem _store = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfterSystem = default!;
    [Dependency] private readonly PopupSystem _popupSystem = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly PuddleSystem _puddleSystem = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SolutionContainerSystem _solutionSystem = default!;
    [Dependency] private readonly HumanoidAppearanceSystem _sharedHuApp = default!;
    [Dependency] private readonly SharedAppearanceSystem _sharedAppearance = default!;
    [Dependency] private readonly DamageableSystem _damageableSystem = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly BodySystem _body = default!;
    [Dependency] private readonly BloodstreamSystem _bloodstreamSystem = default!;
    [Dependency] private readonly GunSystem _gunSystem = default!;
    [Dependency] private readonly TagSystem _tagSystem = default!;
    [Dependency] private readonly RandomHelperSystem _randomHelper = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly SharedTransformSystem _transformSystem = default!;


    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FleshCultistComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<FleshCultistComponent, FleshCultistShopActionEvent>(OnShop);
        SubscribeLocalEvent<FleshCultistComponent, FleshCultistInsulatedImmunityMutationEvent>(OnInsulatedImmunityMutation);
        SubscribeLocalEvent<FleshCultistComponent, FleshCultistPressureImmunityMutationEvent>(OnPressureImmunityMutation);
        SubscribeLocalEvent<FleshCultistComponent, FleshCultistFlashImmunityMutationEvent>(OnFlashImmunityMutation);
        SubscribeLocalEvent<FleshCultistComponent, FleshCultistColdTempImmunityMutationEvent>(OnColdTempImmunityMutation);
        SubscribeLocalEvent<FleshCultistComponent, FleshCultistDevourActionEvent>(OnDevourAction);
        SubscribeLocalEvent<FleshCultistComponent, FleshCultistDevourDoAfterEvent>(OnDevourDoAfter);
        SubscribeLocalEvent<FleshCultistComponent, IsEquippingAttemptEvent>(OnBeingEquippedAttempt);
        SubscribeLocalEvent<FleshCultistComponent, MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<FleshCultistComponent, FleshCultistAbsorbBloodPoolActionEvent>(OnAbsormBloodPoolActionEvent);
        SubscribeLocalEvent<FleshCultistComponent, CloningEvent>(OnCultistCloning);

        InitializeAbilities();
    }

    private void OnCultistCloning(EntityUid uid, FleshCultistComponent component, ref CloningEvent args)
    {
        // If the cultist ate the body we need to visually reset it after cloning
        if (!TryComp<HumanoidAppearanceComponent>(args.Target, out var huAppComponent))
            return;

        var speciesProto = _prototype.Index(huAppComponent.Species);
        var skeletonSprites = _prototype.Index<HumanoidSpeciesBaseSpritesPrototype>(speciesProto.SpriteSet);
        foreach (var (key, id) in skeletonSprites.Sprites)
        {
            _sharedHuApp.SetBaseLayerId(args.Target, key, id, humanoid: huAppComponent);
        }
    }

    private void OnMobStateChanged(EntityUid uid, FleshCultistComponent component, MobStateChangedEvent args)
    {
        switch (args.NewMobState)
        {
            case MobState.Critical:
            {
                EnsureComp<CuffableComponent>(uid);
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
                        if (metaData.EntityPrototype.ID != component.BladeSpawnId &&
                            metaData.EntityPrototype.ID != component.ClawSpawnId &&
                            metaData.EntityPrototype.ID != component.SpikeHandGunSpawnId &&
                            metaData.EntityPrototype.ID != component.FistSpawnId)
                            continue;
                        QueueDel(containerContainedEntity);
                        _audioSystem.PlayPvs(component.SoundMutation, uid, component.SoundMutation.Params);
                    }
                }

                break;
            }
            case MobState.Dead:
            {
                _inventory.TryGetSlotEntity(uid, "shoes", out var shoes);
                if (shoes != null)
                {
                    if (TryComp(shoes, out MetaDataComponent? metaData))
                    {
                        if (metaData.EntityPrototype != null)
                        {
                            if (metaData.EntityPrototype.ID == component.SpiderLegsSpawnId)
                            {
                                EntityManager.DeleteEntity(shoes.Value);
                                _movement.RefreshMovementSpeedModifiers(uid);
                                _audioSystem.PlayPvs(component.SoundMutation, uid, component.SoundMutation.Params);
                            }
                        }
                    }
                }

                _inventory.TryGetSlotEntity(uid, "outerClothing", out var outerClothing);
                if (outerClothing != null)
                {
                    if (TryComp(outerClothing, out MetaDataComponent? metaData))
                    {
                        if (metaData.EntityPrototype != null)
                        {
                            if (metaData.EntityPrototype.ID == component.ArmorSpawnId)
                            {
                                EntityManager.DeleteEntity(outerClothing.Value);
                                _movement.RefreshMovementSpeedModifiers(uid);
                                _audioSystem.PlayPvs(component.SoundMutation, uid, component.SoundMutation.Params);
                            }
                        }
                    }
                }

                ParasiteComesOut(uid, component);
                break;
            }
        }
    }

    private void OnBeingEquippedAttempt(EntityUid uid, FleshCultistComponent component, IsEquippingAttemptEvent args)
    {
        if (args.Slot is not "outerClothing")
            return;
        _inventory.TryGetSlotEntity(uid, "shoes", out var shoes);
        if (shoes == null)
            return;
        if (!TryComp(shoes, out MetaDataComponent? metaData))
            return;
        if (metaData.EntityPrototype == null)
            return;
        if (metaData.EntityPrototype.ID != component.SpiderLegsSpawnId)
            return;
        if (!_tagSystem.HasTag(args.Equipment, "FullBodyOuter"))
            return;
        _popup.PopupEntity(Loc.GetString("flesh-cultist-equiped-outer-clothing-blocked",
            ("Entity", uid)), uid, PopupType.LargeCaution);
        args.Cancel();
    }

    [ValidatePrototypeId<EntityPrototype>] private const string FleshCultistShop = "FleshCultistShop";
    [ValidatePrototypeId<EntityPrototype>] private const string FleshCultistDevour = "FleshCultistDevour";
    [ValidatePrototypeId<EntityPrototype>] private const string FleshCultistAbsorbBloodPool = "FleshCultistAbsorbBloodPool";

    private void OnStartup(EntityUid uid, FleshCultistComponent component, ComponentStartup args)
    {
        EnsureComp<StoreComponent>(uid);
        //update the icon
        ChangeParasiteHunger(uid, 0, component);



        _store.TryAddCurrency(new Dictionary<string, FixedPoint2>
            { {component.StolenCurrencyPrototype, component.StartingEvolutionPoint} }, uid);


        _action.AddAction(uid, ref component.FleshCultistShop, FleshCultistShop);
        _action.AddAction(uid, ref component.FleshCultistDevour, FleshCultistDevour);
        _action.AddAction(uid, ref component.FleshCultistAbsorbBloodPool, FleshCultistAbsorbBloodPool);
    }

    private void OnInsulatedImmunityMutation(EntityUid uid, FleshCultistComponent component,
        FleshCultistInsulatedImmunityMutationEvent args)
    {
        EnsureComp<InsulatedComponent>(uid);
    }


    private void OnPressureImmunityMutation(EntityUid uid, FleshCultistComponent component,
        FleshCultistPressureImmunityMutationEvent args)
    {
        EnsureComp<PressureImmunityComponent>(uid);
    }

    private void OnFlashImmunityMutation(EntityUid uid, FleshCultistComponent component,
        FleshCultistFlashImmunityMutationEvent args)
    {
        EnsureComp<FlashImmunityComponent>(uid);
    }

    private void OnColdTempImmunityMutation(EntityUid uid, FleshCultistComponent component,
        FleshCultistColdTempImmunityMutationEvent args)
    {
        if (TryComp<TemperatureComponent>(uid, out var tempComponent))
        {
            tempComponent.ColdDamageThreshold = 0;
        }
    }

    private void OnShop(EntityUid uid, FleshCultistComponent component, FleshCultistShopActionEvent args)
    {
        if (!TryComp<StoreComponent>(uid, out var store))
            return;
        _store.ToggleUi(uid, uid, store);
    }

    private bool ChangeParasiteHunger(EntityUid uid, FixedPoint2 amount, FleshCultistComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return false;

        component.Hunger += amount;

        if (TryComp<StoreComponent>(uid, out var store))
            _store.UpdateUserInterface(uid, uid, store);

        _alerts.ShowAlert(uid, AlertType.MutationPoint, (short) Math.Clamp(Math.Round(component.Hunger.Float() / 10f), 0, 16));

        return true;
    }

    private void OnDevourAction(EntityUid uid, FleshCultistComponent component, FleshCultistDevourActionEvent args)
    {
        if (args.Handled)
            return;

        var target = args.Target;

        if (!TryComp<MobStateComponent>(target, out var targetState))
            return;
        if (!TryComp<BloodstreamComponent>(target, out var bloodstream))
            return;
        var hasAppearance = false;
        {
            switch (targetState.CurrentState)
            {
                case MobState.Dead:
                    if (EntityManager.TryGetComponent(target, out HumanoidAppearanceComponent? humanoidAppearance))
                    {
                        if (!component.SpeciesWhitelist.Contains(humanoidAppearance.Species))
                        {
                            _popupSystem.PopupEntity(
                                Loc.GetString("flesh-cultist-devout-target-not-have-flesh"),
                                uid, uid);
                            return;
                        }

                        if (TryComp<FixturesComponent>(target, out var fixturesComponent))
                        {
                            if (fixturesComponent.Fixtures["fix1"].Density <= 60)
                            {
                                _popupSystem.PopupEntity(
                                    Loc.GetString("flesh-cultist-devout-target-invalid"),
                                    uid, uid);
                                return;
                            }
                        }

                        hasAppearance = true;
                    }
                    else
                    {
                        if (bloodstream.BloodReagent != "Blood")
                        {
                            _popupSystem.PopupEntity(
                                Loc.GetString("flesh-cultist-devout-target-not-have-flesh"),
                                uid, uid);
                            return;
                        }
                        if (bloodstream.BloodMaxVolume < 30)
                        {
                            _popupSystem.PopupEntity(
                                Loc.GetString("flesh-cultist-devout-target-invalid"),
                                uid, uid);
                            return;
                        }
                    }
                    var saturation = MatchSaturation(bloodstream.BloodMaxVolume.Value / 100, hasAppearance);
                    if (component.Hunger + saturation >= component.MaxHunger)
                    {
                        _popupSystem.PopupEntity(
                            Loc.GetString("flesh-cultist-devout-not-hungry"),
                            uid, uid);
                        return;
                    }
                    _doAfterSystem.TryStartDoAfter(new DoAfterArgs(EntityManager, uid, component.DevourTime,
                        new FleshCultistDevourDoAfterEvent(), uid, target: target, used: uid)
                    {
                        BreakOnTargetMove = true,
                        BreakOnUserMove = true,
                    });
                    args.Handled = true;
                    break;

                case MobState.Invalid:
                case MobState.Critical:
                case MobState.Alive:
                default:
                    _popupSystem.PopupEntity(
                        Loc.GetString("flesh-cultist-devout-target-alive"),
                        uid, uid);
                    break;
            }
        }
    }

    private void OnDevourDoAfter(EntityUid uid, FleshCultistComponent component, FleshCultistDevourDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled)
            return;

        if (args.Args.Target == null)
            return;

        if (!TryComp<BloodstreamComponent>(args.Args.Target.Value, out var bloodstream))
            return;

        var hasAppearance = false;

        var xform = Transform(args.Args.Target.Value);
        var coordinates = xform.Coordinates;
        var audio = AudioParams.Default.WithVariation(0.025f);
        _audio.PlayPvs(component.DevourSound, args.Args.Target.Value, audio);
        _popupSystem.PopupEntity(Loc.GetString("flesh-cultist-devour-target",
                ("Entity", uid), ("Target", args.Args.Target)), uid);

        if (TryComp<HumanoidAppearanceComponent>(args.Args.Target, out var humanoidAppearanceComponent))
        {
            if (TryComp(args.Args.Target.Value, out ContainerManagerComponent? container))
            {
                foreach (var cont in container.GetAllContainers().ToArray())
                {
                    foreach (var ent in cont.ContainedEntities.ToArray())
                    {
                        if (HasComp<BodyPartComponent>(ent))
                        {
                            continue;
                        }
                        cont.Remove(ent, EntityManager, force: true);
                        _transformSystem.SetCoordinates(ent, coordinates);
                        _randomHelper.RandomOffset(ent, 0.25f);
                    }
                }
            }

            if (TryComp<BodyComponent>(args.Args.Target, out var bodyComponent))
            {
                var parts = _body.GetBodyChildren(args.Args.Target, bodyComponent).ToArray();

                foreach (var part in parts)
                {
                    if (part.Component.PartType == BodyPartType.Head)
                        continue;

                    if (part.Component.PartType == BodyPartType.Torso)
                    {
                        foreach (var organ in _body.GetPartOrgans(part.Id, part.Component))
                        {
                            _body.RemoveOrgan(organ.Id, organ.Component);
                        }
                    }
                    else
                    {
                        QueueDel(part.Id);
                    }
                }
            }

            _bloodstreamSystem.TryModifyBloodLevel(args.Args.Target.Value, -300);

            var skeletonSprites = _proto.Index<HumanoidSpeciesBaseSpritesPrototype>("MobSkeletonSprites");
            foreach (var (key, id) in skeletonSprites.Sprites)
            {
                if (key != HumanoidVisualLayers.Head)
                {
                    _sharedHuApp.SetBaseLayerId(args.Args.Target.Value, key, id, humanoid: humanoidAppearanceComponent);
                }
            }

            if (TryComp<FixturesComponent>(args.Args.Target, out var fixturesComponent))
            {
                _physics.SetDensity(args.Args.Target.Value, "fix1", fixturesComponent.Fixtures["fix1"], 50);
            }

            if (TryComp<AppearanceComponent>(args.Args.Target, out var appComponent))
            {
                _sharedAppearance.SetData(args.Args.Target.Value, DamageVisualizerKeys.Disabled, true, appComponent);
                _damageableSystem.TryChangeDamage(args.Args.Target.Value, new DamageSpecifier(){DamageDict = {{"Slash", 100}}});
            }

            hasAppearance = true;
        }

        var saturation = MatchSaturation(bloodstream.BloodMaxVolume.Value / 100, hasAppearance);
        var evolutionPoint = MatchEvolutionPoint(bloodstream.BloodMaxVolume.Value / 100, hasAppearance);
        var healPoint = MatchHealPoint(bloodstream.BloodMaxVolume.Value / 100, hasAppearance);
        var tempSol = new Solution() { MaxVolume = 5 };

        tempSol.AddSolution(bloodstream.BloodSolution, _proto);

        if (_puddleSystem.TrySpillAt(args.Args.Target.Value, tempSol.SplitSolution(50), out var puddleUid))
        {
            if (TryComp<DnaComponent>(args.Args.Target.Value, out var dna))
            {
                var comp = EnsureComp<ForensicsComponent>(puddleUid);
                comp.DNAs.Add(dna.DNA);
            }
        }

        if (!hasAppearance)
        {
            QueueDel(args.Args.Target.Value);
        }

        if (_solutionSystem.TryGetInjectableSolution(uid, out var injectableSolution))
        {
            var transferSolution = new Solution();
            foreach (var reagent in component.HealDevourReagents)
            {
                transferSolution.AddReagent(reagent.Reagent, reagent.Quantity * healPoint);
            }
            _solutionSystem.TryAddSolution(uid, injectableSolution, transferSolution);
        }

        component.Hunger += saturation;
        _store.TryAddCurrency(new Dictionary<string, FixedPoint2>
            { {component.StolenCurrencyPrototype, evolutionPoint} }, uid);
    }

    private int MatchSaturation(int bloodVolume, bool hasAppearance)
    {
        if (hasAppearance)
        {
            return 80;
        }
        return bloodVolume switch
        {
            >= 300 => 60,
            >= 150 => 45,
            >= 100 => 40,
            _ => 15
        };
    }

    private int MatchEvolutionPoint(int bloodVolume, bool hasAppearance)
    {
        if (hasAppearance)
        {
            return 30;
        }
        return bloodVolume switch
        {
            >= 300 => 20,
            >= 150 => 15,
            >= 100 => 10,
            _ => 5
        };
    }

    private float MatchHealPoint(int bloodVolume, bool hasAppearance)
    {
        if (hasAppearance)
        {
            return 1;
        }
        return bloodVolume switch
        {
            >= 300 => 0.8f,
            >= 150 => 0.6f,
            >= 100 => 0.4f,
            _ => 0.2f
        };
    }

    private bool ParasiteComesOut(EntityUid uid, FleshCultistComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return false;

        var xform = Transform(uid);
        var coordinates = xform.Coordinates;

        var abommob = Spawn(component.FleshMutationMobId, coordinates);
        if (TryComp<MindContainerComponent>(uid, out var mindComp) && mindComp.HasMind)
            EntityManager.System<MindSystem>().TransferTo(mindComp.Mind!.Value, abommob, true);
            //mindComp.Mind?.TransferTo(abommob, ghostCheckOverride: true);

        _popup.PopupEntity(Loc.GetString("flesh-pudge-transform-user", ("EntityTransform", uid)),
            uid, uid, PopupType.LargeCaution);
        _popup.PopupEntity(Loc.GetString("flesh-pudge-transform-others",
            ("Entity", uid), ("EntityTransform", abommob)), abommob, Filter.PvsExcept(abommob),
            true, PopupType.LargeCaution);

        var audio = AudioParams.Default.WithVariation(0.025f);
        _audio.PlayPvs(component.SoundMutation, uid, audio);

        if (TryComp(uid, out ContainerManagerComponent? container))
        {
            foreach (var cont in container.GetAllContainers().ToArray())
            {
                foreach (var ent in cont.ContainedEntities.ToArray())
                {
                    {
                        if (HasComp<BodyPartComponent>(ent))
                            continue;
                        if (HasComp<UnremoveableComponent>(ent))
                            continue;
                        cont.Remove(ent, EntityManager, force: true);
                        _transformSystem.SetCoordinates(ent, coordinates);
                        _randomHelper.RandomOffset(ent, 0.25f);
                    }
                }
            }
        }

        if (TryComp<BloodstreamComponent>(uid, out var bloodstream))
        {
            var tempSol = new Solution() { MaxVolume = 5 };

            tempSol.AddSolution(bloodstream.BloodSolution, _proto);

            if (_puddleSystem.TrySpillAt(uid, tempSol.SplitSolution(50), out var puddleUid))
            {
                if (TryComp<DnaComponent>(uid, out var dna))
                {
                    var comp = EnsureComp<ForensicsComponent>(puddleUid);
                    comp.DNAs.Add(dna.DNA);
                }
            }
        }

        QueueDel(uid);
        return true;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var q = EntityQueryEnumerator<FleshCultistComponent>();
        while (q.MoveNext(out var owner, out var rev))
        {
            rev.Accumulator += frameTime;

            if (rev.Accumulator <= 1)
                continue;

            rev.Accumulator -= 1;

            if (rev.Hunger <= 40)
            {
                rev.AccumulatorStarveNotify += 1;
                if (rev.AccumulatorStarveNotify > 30)
                {
                    rev.AccumulatorStarveNotify = 0;
                    _popup.PopupEntity(Loc.GetString("flesh-cultist-hungry"),
                        owner, owner, PopupType.Large);
                }
            }

            if (rev.Hunger < 0)
            {
                ParasiteComesOut(owner, rev);
            }

            ChangeParasiteHunger(owner, rev.HungerСonsumption, rev);
        }
    }

    private void OnAbsormBloodPoolActionEvent(EntityUid uid, FleshCultistComponent component,
        FleshCultistAbsorbBloodPoolActionEvent args)
    {
        if (args.Handled)
            return;

        var xform = Transform(uid);
        var puddles = new ValueList<(EntityUid Entity, string Solution)>();
        puddles.Clear();
        foreach (var entity in _lookup.GetEntitiesInRange(xform.MapPosition, 0.5f))
        {
            if (TryComp<PuddleComponent>(entity, out var puddle))
            {
                puddles.Add((entity, puddle.SolutionName));
            }
        }

        if (puddles.Count == 0)
        {
            _popup.PopupEntity(Loc.GetString("flesh-cultist-not-find-puddles"),
                uid, uid, PopupType.Large);
            return;
        }

        var totalBloodQuantity = new float();

        foreach (var (puddle, solution) in puddles)
        {
            if (!_solutionSystem.TryGetSolution(puddle, solution, out var puddleSolution))
            {
                continue;
            }
            var hasImpurities = false;
            var pudleBloodQuantity = new FixedPoint2();
            foreach (var puddleSolutionContent in puddleSolution.Contents.ToArray())
            {
                if (puddleSolutionContent.Reagent.Prototype != "Blood")
                {
                    hasImpurities = true;
                }
                else
                {
                    pudleBloodQuantity += puddleSolutionContent.Quantity;
                }
            }
            if (hasImpurities)
                continue;
            totalBloodQuantity += pudleBloodQuantity.Float();
            QueueDel(puddle);
        }

        if (totalBloodQuantity == 0)
        {
            _popup.PopupEntity(Loc.GetString("flesh-cultist-cant-absorb-puddle"),
                uid, uid, PopupType.Large);
            return;
        }

        _audioSystem.PlayPvs(component.BloodAbsorbSound, uid, component.BloodAbsorbSound.Params);
        _popup.PopupEntity(Loc.GetString("flesh-cultist-absorb-puddle", ("Entity", uid)),
            uid, uid, PopupType.Large);

        var transferSolution = new Solution();
        foreach (var reagent in component.HealBloodAbsorbReagents.ToArray())
        {
            transferSolution.AddReagent(reagent.Reagent, reagent.Quantity * (totalBloodQuantity / 10));
        }
        if (_solutionSystem.TryGetInjectableSolution(uid, out var injectableSolution))
        {
            _solutionSystem.TryAddSolution(uid, injectableSolution, transferSolution);
        }
        args.Handled = true;
    }
}
