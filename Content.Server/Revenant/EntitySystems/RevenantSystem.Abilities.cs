using Content.Shared.Popups;
using Content.Shared.Damage;
using Content.Shared.Revenant;
using Robust.Shared.Random;
using Content.Shared.Tag;
using Content.Shared.Storage.Components;
using Content.Server.Ghost;
using Robust.Shared.Physics;
using Content.Shared.Throwing;
using Content.Server.Storage.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Item;
using Content.Shared.Bed.Sleep;
using System.Linq;
using System.Numerics;
// start-backmen: revenant-abilities
using Content.Server._Impstation.Revenant.Components;
using Content.Server._Impstation.Revenant.EntitySystems;
using Content.Server.Backmen.Disease;
using Content.Shared._Impstation.Revenant;
using Content.Shared._Impstation.Revenant.Components;
using Content.Shared.Backmen.Disease;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Movement.Components;
using Content.Shared.StatusEffectNew;
using Content.Shared.Flash;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;
using Robust.Server.Player;
// end-backmen: revenant-abilities
using Content.Server.Revenant.Components;
using Content.Shared.Physics;
using Content.Shared.DoAfter;
using Content.Shared.Emag.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Humanoid;
using Content.Shared.Light.Components;
using Content.Shared.Maps;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Revenant.Components;
using Robust.Shared.Physics.Components;
using Robust.Shared.Utility;
using Robust.Shared.Map.Components;
using Content.Shared.Whitelist;
using Robust.Shared.Prototypes;

namespace Content.Server.Revenant.EntitySystems;

public sealed partial class RevenantSystem
{
    [Dependency] private EmagSystem _emagSystem = default!;
    [Dependency] private ThrowingSystem _throwing = default!;
    [Dependency] private EntityStorageSystem _entityStorage = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private MobThresholdSystem _mobThresholdSystem = default!;
    [Dependency] private GhostSystem _ghost = default!;
    [Dependency] private TileSystem _tile = default!;
    [Dependency] private EntityWhitelistSystem _whitelistSystem = default!;
    [Dependency] private SharedMapSystem _mapSystem = default!;
    // start-backmen: revenant-abilities
    [Dependency] private SharedHandsSystem _handsSystem = default!;
    [Dependency] private RevenantAnimatedSystem _revenantAnimated = default!;
    [Dependency] private SharedAudioSystem _audioSystem = default!;
    [Dependency] private IPlayerManager _players = default!;
    [Dependency] private DiseaseSystem _disease = default!;
    [Dependency] private EntityQuery<DiseaseCarrierComponent> _diseaseCarrierQuery = default!;
    // end-backmen: revenant-abilities

    [Dependency] private EntityQuery<TagComponent> _tagQuery = default!;
    [Dependency] private EntityQuery<EntityStorageComponent> _entityStorageQuery = default!;
    [Dependency] private EntityQuery<ItemComponent> _itemQuery = default!;
    [Dependency] private EntityQuery<PoweredLightComponent> _poweredLightQuery = default!;
    [Dependency] private EntityQuery<MobStateComponent> _mobStateQuery = default!;

    private static readonly ProtoId<TagPrototype> WindowTag = "Window";

    private void InitializeAbilities()
    {
        SubscribeLocalEvent<RevenantComponent, UserActivateInWorldEvent>(OnInteract);
        SubscribeLocalEvent<RevenantComponent, SoulEvent>(OnSoulSearch);
        SubscribeLocalEvent<RevenantComponent, HarvestEvent>(OnHarvest);

        SubscribeLocalEvent<RevenantComponent, RevenantDefileActionEvent>(OnDefileAction);
        SubscribeLocalEvent<RevenantComponent, RevenantOverloadLightsActionEvent>(OnOverloadLightsAction);
        SubscribeLocalEvent<RevenantComponent, RevenantBlightActionEvent>(OnBlightAction);
        SubscribeLocalEvent<RevenantComponent, RevenantMalfunctionActionEvent>(OnMalfunctionAction);
        // start-backmen: revenant-abilities
        SubscribeLocalEvent<RevenantComponent, RevenantBloodWritingEvent>(OnBloodWritingAction);
        SubscribeLocalEvent<RevenantComponent, RevenantAnimateEvent>(OnAnimateAction);
        SubscribeLocalEvent<RevenantComponent, RevenantHauntActionEvent>(OnHauntAction);
        // end-backmen: revenant-abilities
    }

    private void OnInteract(EntityUid uid, RevenantComponent component, UserActivateInWorldEvent args)
    {
        if (args.Handled)
            return;

        if (args.Target == args.User)
            return;
        var target = args.Target;

        if (HasComp<PoweredLightComponent>(target))
        {
            args.Handled = _ghost.DoGhostBooEvent(target);
            return;
        }

        if (!_mobStateQuery.HasComp(target) || !HasComp<HumanoidProfileComponent>(target) || HasComp<RevenantComponent>(target))
            return;

        args.Handled = true;
        if (!TryComp<EssenceComponent>(target, out var essence) || !essence.SearchComplete)
        {
            EnsureComp<EssenceComponent>(target);
            BeginSoulSearchDoAfter(uid, target, component);
        }
        else
        {
            BeginHarvestDoAfter(uid, target, component, essence);
        }

        args.Handled = true;
    }

    private void BeginSoulSearchDoAfter(EntityUid uid, EntityUid target, RevenantComponent revenant)
    {
        var searchDoAfter = new DoAfterArgs(EntityManager, uid, revenant.SoulSearchDuration, new SoulEvent(), uid, target: target)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            DistanceThreshold = 2
        };

        if (!_doAfter.TryStartDoAfter(searchDoAfter))
            return;

        _popup.PopupEntity(Loc.GetString("revenant-soul-searching", ("target", target)), uid, uid, PopupType.Medium);
    }

    private void OnSoulSearch(EntityUid uid, RevenantComponent component, SoulEvent args)
    {
        if (args.Handled || args.Cancelled)
            return;

        if (!TryComp<EssenceComponent>(args.Args.Target, out var essence))
            return;
        essence.SearchComplete = true;

        string message;
        switch (essence.EssenceAmount)
        {
            case <= 45:
                message = "revenant-soul-yield-low";
                break;
            case >= 90:
                message = "revenant-soul-yield-high";
                break;
            default:
                message = "revenant-soul-yield-average";
                break;
        }
        _popup.PopupEntity(Loc.GetString(message, ("target", args.Args.Target)), args.Args.Target.Value, uid, PopupType.Medium);

        args.Handled = true;
    }

    private void BeginHarvestDoAfter(EntityUid uid, EntityUid target, RevenantComponent revenant, EssenceComponent essence)
    {
        if (essence.Harvested)
        {
            _popup.PopupEntity(Loc.GetString("revenant-soul-harvested"), target, uid, PopupType.SmallCaution);
            return;
        }

        if (_mobStateQuery.TryComp(target, out var mobstate) && mobstate.CurrentState == MobState.Alive && !HasComp<SleepingComponent>(target))
        {
            _popup.PopupEntity(Loc.GetString("revenant-soul-too-powerful"), target, uid);
            return;
        }

        if(_physics.GetEntitiesIntersectingBody(uid, (int) CollisionGroup.Impassable).Count > 0)
        {
            _popup.PopupEntity(Loc.GetString("revenant-in-solid"), uid, uid);
            return;
        }

        var doAfter = new DoAfterArgs(EntityManager, uid, revenant.HarvestDebuffs.X, new HarvestEvent(), uid, target: target)
        {
            DistanceThreshold = 2,
            BreakOnMove = true,
            BreakOnDamage = true,
            RequireCanInteract = false, // stuns itself
        };

        if (!_doAfter.TryStartDoAfter(doAfter))
            return;

        _appearance.SetData(uid, RevenantVisuals.Harvesting, true);

        _popup.PopupEntity(Loc.GetString("revenant-soul-begin-harvest", ("target", target)),
            target, PopupType.Large);

        TryUseAbility(uid, revenant, 0, revenant.HarvestDebuffs);
    }

    private void OnHarvest(EntityUid uid, RevenantComponent component, HarvestEvent args)
    {
        if (args.Cancelled)
        {
            _appearance.SetData(uid, RevenantVisuals.Harvesting, false);
            return;
        }

        if (args.Handled || args.Args.Target == null)
            return;

        _appearance.SetData(uid, RevenantVisuals.Harvesting, false);

        if (!TryComp<EssenceComponent>(args.Args.Target, out var essence))
            return;

        _popup.PopupEntity(Loc.GetString("revenant-soul-finish-harvest", ("target", args.Args.Target)),
            args.Args.Target.Value, PopupType.LargeCaution);

        essence.Harvested = true;
        ChangeEssenceAmount(uid, essence.EssenceAmount, component);
        _store.TryAddCurrency(new Dictionary<string, FixedPoint2>
            { {component.StolenEssenceCurrencyPrototype, essence.EssenceAmount} }, uid);

        if (!_mobStateQuery.HasComp(args.Args.Target))
            return;

        if (_mobState.IsAlive(args.Args.Target.Value) || _mobState.IsCritical(args.Args.Target.Value))
        {
            _popup.PopupEntity(Loc.GetString("revenant-max-essence-increased"), uid, uid);
            component.EssenceRegenCap += component.MaxEssenceUpgradeAmount;
        }

        //KILL THEMMMM

        if (!_mobThresholdSystem.TryGetThresholdForState(args.Args.Target.Value, MobState.Dead, out var damage))
            return;
        DamageSpecifier dspec = new();
        dspec.DamageDict.Add("Cold", damage.Value);
        _damage.ChangeDamage(args.Args.Target.Value, dspec, true, origin: uid);

        args.Handled = true;
    }

    private void OnDefileAction(EntityUid uid, RevenantComponent component, RevenantDefileActionEvent args)
    {
        if (args.Handled)
            return;

        if (!TryUseAbility(uid, component, component.DefileCost, component.DefileDebuffs))
            return;

        args.Handled = true;

        var xform = Transform(uid);
        if (!TryComp<MapGridComponent>(xform.GridUid, out var map))
            return;
        var tiles = _mapSystem.GetTilesIntersecting(
            xform.GridUid.Value,
            map,
            Box2.CenteredAround(_transformSystem.GetWorldPosition(xform),
            new Vector2(component.DefileRadius * 2, component.DefileRadius)))
            .ToArray();

        _random.Shuffle(tiles);

        for (var i = 0; i < component.DefileTilePryAmount; i++)
        {
            if (!tiles.TryGetValue(i, out var value))
                continue;
            _tile.PryTile(value);
        }

        var lookup = _lookup.GetEntitiesInRange(uid, component.DefileRadius, LookupFlags.Approximate | LookupFlags.Static);
        foreach (var ent in lookup)
        {
            //break windows
            if (_tagQuery.HasComponent(ent) && _tag.HasTag(ent, WindowTag))
            {
                //hardcoded damage specifiers til i die.
                var dspec = new DamageSpecifier();
                dspec.DamageDict.Add("Structural", 60);
                _damage.TryChangeDamage(ent, dspec, origin: uid);
            }

            if (!_random.Prob(component.DefileEffectChance))
                continue;

            //randomly opens some lockers and such.
            if (_entityStorageQuery.TryGetComponent(ent, out var entstorecomp))
                _entityStorage.OpenStorage(ent, entstorecomp);

            //chucks shit
            if (_itemQuery.HasComponent(ent) &&
                TryComp<PhysicsComponent>(ent, out var phys) && phys.BodyType != BodyType.Static)
                _throwing.TryThrow(ent, _random.NextAngle().ToWorldVec());

            //flicker lights
            if (_poweredLightQuery.HasComponent(ent))
                _ghost.DoGhostBooEvent(ent);
        }
    }

    private void OnOverloadLightsAction(EntityUid uid, RevenantComponent component, RevenantOverloadLightsActionEvent args)
    {
        if (args.Handled)
            return;

        if (!TryUseAbility(uid, component, component.OverloadCost, component.OverloadDebuffs))
            return;

        args.Handled = true;

        var xform = Transform(uid);
        var lookup = _lookup.GetEntitiesInRange(uid, component.OverloadRadius);
        //TODO: feels like this might be a sin and a half
        foreach (var ent in lookup)
        {
            if (!_mobStateQuery.HasComp(ent) || !_mobState.IsAlive(ent))
                continue;

            var nearbyLights = _lookup.GetEntitiesInRange(ent, component.OverloadZapRadius)
                .Where(e => _poweredLightQuery.HasComp(e) && !HasComp<RevenantOverloadedLightsComponent>(e) &&
                            _interact.InRangeUnobstructed(e, uid, -1)).ToArray();

            if (!nearbyLights.Any())
                continue;

            //get the closest light
            var allLight = nearbyLights.OrderBy(e =>
                Transform(e).Coordinates.TryDistance(EntityManager, xform.Coordinates, out var dist) ? component.OverloadZapRadius : dist);
            var comp = EnsureComp<RevenantOverloadedLightsComponent>(allLight.First());
            comp.Target = ent; //who they gon fire at?
        }
    }

    private void OnBlightAction(EntityUid uid, RevenantComponent component, RevenantBlightActionEvent args)
    {
        if (args.Handled)
            return;

        if (!TryUseAbility(uid, component, component.BlightCost, component.BlightDebuffs))
            return;

        args.Handled = true;
        // start-backmen: revenant-blight
        foreach (var ent in _lookup.GetEntitiesInRange(uid, component.BlightRadius))
        {
            if (_diseaseCarrierQuery.TryComp(ent, out var carrier))
                _disease.TryAddDisease(ent, component.BlightDiseasePrototypeId, carrier);
        }
        // end-backmen: revenant-blight
    }

    private void OnMalfunctionAction(EntityUid uid, RevenantComponent component, RevenantMalfunctionActionEvent args)
    {
        if (args.Handled)
            return;

        if (!TryUseAbility(uid, component, component.MalfunctionCost, component.MalfunctionDebuffs))
            return;

        args.Handled = true;

        foreach (var ent in _lookup.GetEntitiesInRange(uid, component.MalfunctionRadius))
        {
            if (_whitelistSystem.IsWhitelistFail(component.MalfunctionWhitelist, ent) ||
                _whitelistSystem.IsWhitelistPass(component.MalfunctionBlacklist, ent))
                continue;

            _emagSystem.TryEmagEffect(uid, uid, ent);
        }
    }

    // start-backmen: revenant-abilities
    private void OnHauntAction(EntityUid uid, RevenantComponent comp, RevenantHauntActionEvent args)
    {
        if (args.Handled)
            return;

        if (!TryUseAbility(uid, comp, 0, comp.HauntDebuffs))
            return;

        args.Handled = true;

        // Prefer range lookup over Filter.Pvs(revenant): ghost visibility can make PVS feel empty,
        // and we need every nearby living player for flash + jumpscare.
        var witnessFilter = Filter.Empty();
        var witnessNets = new HashSet<NetEntity>();
        var newHaunts = 0;

        foreach (var ent in _lookup.GetEntitiesInRange(uid, comp.HauntRange))
        {
            if (ent == uid)
                continue;

            if (!HasComp<MobStateComponent>(ent) || !HasComp<MobMoverComponent>(ent) || HasComp<RevenantComponent>(ent))
                continue;

            if (!_interact.InRangeUnobstructed(uid, ent, -1, collisionMask: CollisionGroup.Impassable))
                continue;

            // Already haunted: skip scare entirely until the debuff expires.
            if (_status.HasStatusEffect(ent, RevenantStatusEffects.Haunted))
                continue;

            // Flash is a separate short StatusEffectFlashed — not on Haunted, or blindness lasts 3 minutes.
            _stun.TryUpdateParalyzeDuration(ent, comp.HauntStunDuration);
            _status.TryAddStatusEffectDuration(ent, SharedFlashSystem.FlashedKey, comp.HauntFlashDuration);

            if (_players.TryGetSessionByEntity(ent, out var session))
            {
                witnessFilter.AddPlayer(session);
                RaiseNetworkEvent(new RevenantHauntJumpscareEvent(comp.HauntJumpscareDuration), session);
            }

            witnessNets.Add(GetNetEntity(ent));

            if (_status.TryAddStatusEffectDuration(ent, RevenantStatusEffects.Haunted, comp.HauntHauntedDuration))
                newHaunts++;
        }

        if (witnessFilter.Count > 0)
            _audioSystem.PlayGlobal(comp.HauntSound, witnessFilter, true);

        if (newHaunts <= 0)
            return;

        if (_status.TryAddStatusEffectDuration(uid, RevenantStatusEffects.EssenceRegen, out var regenEnt, comp.HauntEssenceRegenDuration))
        {
            var regen = EnsureComp<RevenantRegenModifierStatusEffectComponent>(regenEnt.Value);
            regen.Witnesses = witnessNets;
            regen.NewHaunts = newHaunts;
            Dirty(regenEnt.Value, regen);

            if (_mind.TryGetMind(uid, out _, out var mind)
                && mind.UserId != null
                && _players.TryGetSessionById(mind.UserId.Value, out var session))
                RaiseNetworkEvent(new RevenantHauntWitnessEvent(witnessNets), session);

            _store.TryAddCurrency(
                new Dictionary<string, FixedPoint2> { { comp.StolenEssenceCurrencyPrototype, comp.HauntStolenEssencePerWitness * newHaunts } },
                uid);
        }
    }
    // end-backmen: revenant-abilities

    private void OnBloodWritingAction(EntityUid uid, RevenantComponent component, RevenantBloodWritingEvent args)
    {
        if (args.Handled)
            return;

        if (!TryComp<HandsComponent>(uid, out var hands))
            return;

        if (component.BloodCrayon != null)
        {
            _handsSystem.RemoveHand((uid, hands), "crayon");
            QueueDel(component.BloodCrayon);
            component.BloodCrayon = null;
        }
        else
        {
            _handsSystem.AddHand((uid, hands), "crayon", HandLocation.Middle);
            var crayon = Spawn("CrayonBlood");
            component.BloodCrayon = crayon;
            _handsSystem.DoPickup(uid, "crayon", crayon, hands);
            EnsureComp<BloodCrayonComponent>(crayon);
        }

        args.Handled = true;
    }

    private void OnAnimateAction(EntityUid uid, RevenantComponent comp, RevenantAnimateEvent args)
    {
        if (args.Handled)
            return;

        if (_revenantAnimated.CanAnimateObject(args.Target)
            && TryUseAbility(uid, comp, comp.AnimateCost, comp.AnimateDebuffs))
        {
            _revenantAnimated.TryAnimateObject(args.Target, comp.AnimateTime, (uid, comp));
            args.Handled = true;
        }
    }
    // end-backmen: revenant-abilities
}
