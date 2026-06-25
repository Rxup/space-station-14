using System.Linq;
using Content.Shared.Body.Components;
using Content.Server.DoAfter;
using Content.Server.Popups;
using Content.Shared.Backmen.CCVar;
using Content.Shared.Backmen.Surgery.Body.Events;
using Content.Shared.Backmen.Surgery.Body.Organs;
using Content.Shared.Backmen.Surgery.Consciousness;
using Content.Shared.Backmen.Surgery.Consciousness.Components;
using Content.Shared.Backmen.Surgery.Consciousness.Systems;
using Content.Shared.Backmen.Surgery.Pain;
using Content.Shared.Backmen.Surgery.Pain.Components;
using Content.Shared.Backmen.Surgery.Pain.Systems;
using Content.Shared.Backmen.Surgery.Traumas;
using Content.Shared.Backmen.Surgery.Traumas.Systems;
using Content.Shared.Backmen.Surgery.Wounds;
using Content.Shared.Backmen.Targeting;
using Content.Shared.Body.Part;
using Content.Shared.Body;
using Content.Shared.Body.Events;
using Content.Shared.Body.Systems;
using Content.Shared.Backmen.Body.Systems;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.Humanoid;
using Content.Shared.Interaction;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Rejuvenate;
using Content.Shared.Standing;
using Content.Shared.Zombies;
using JetBrains.Annotations;
using Robust.Shared.Audio;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.Backmen.Surgery.Consciousness.Systems;

public sealed partial class ServerConsciousnessSystem : ConsciousnessSystem
{
    [Dependency] private IConfigurationManager _cfg = default!;

    [Dependency] private DoAfterSystem _doAfter = default!;
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private TraumaSystem _trauma = default!;
    [Dependency] private PainSystem _pain = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private StandingStateSystem _standing = default!;
    [Dependency] private MobThresholdSystem _mobThresholds = default!;


    private float _cprTraumaChance = 0.1f;

    private static readonly ProtoId<DamageTypePrototype> AsphyxiationDamageType = "Asphyxiation";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ConsciousnessComponent, ComponentInit>(OnConsciousnessInit);
        SubscribeLocalEvent<ConsciousnessComponent, MapInitEvent>(OnConsciousnessMapInit);
        SubscribeLocalEvent<ConsciousnessComponent, DamageableWoundApplyEvent>(OnConsciousnessDamaged);

        SubscribeLocalEvent<ConsciousnessComponent, InteractHandEvent>(OnConsciousnessInteract);
        SubscribeLocalEvent<ConsciousnessComponent, CprDoAfterEvent>(OnCprDoAfter);

        // To prevent people immediately falling down as rejuvenated
        SubscribeLocalEvent<ConsciousnessComponent, RejuvenateEvent>(OnRejuvenate, after: [typeof(BkmBodySharedSystem)]);
        SubscribeLocalEvent<ConsciousnessComponent, HandleUnhandledWoundsEvent>(OnHandleUnhandledDamage);
        SubscribeLocalEvent<ConsciousnessComponent, DamageableGetHealableDamageEvent>(OnGetHealableDamage);

        SubscribeLocalEvent<ConsciousnessRequiredComponent, BodyPartAddedEvent>(OnBodyPartAdded);
        SubscribeLocalEvent<ConsciousnessRequiredComponent, BodyPartRemovedEvent>(OnBodyPartRemoved);

        SubscribeLocalEvent<ConsciousnessRequiredComponent, OrganAddedToBodyEvent>(OnOrganAdded);
        SubscribeLocalEvent<ConsciousnessRequiredComponent, OrganRemovedFromBodyEvent>(OnOrganRemoved);

        SubscribeLocalEvent<ConsciousnessComponent, EntityZombifiedEvent>(OnZombified);

        Subs.CVar(_cfg, CCVars.CprTraumaChance, value => _cprTraumaChance = value, true);
    }

    private void OnZombified(Entity<ConsciousnessComponent> ent, ref EntityZombifiedEvent args)
    {
        Body.ForceRestoreBody(ent.Owner, true);
        RemComp<ConsciousnessComponent>(ent);
        _damageable.ClearAllDamage(ent.Owner);
        MobStateSys.ChangeMobState(ent, MobState.Alive);
        _standing.Stand(ent, force: true);
    }

    private const string NerveSystemIdentifier = "nerveSystem";

    private void OnConsciousnessDamaged(
        EntityUid uid,
        ConsciousnessComponent component,
        ref DamageableWoundApplyEvent args)
    {
        if (args.Handled)
            return;

        var actuallyInducedDamage = new DamageSpecifier(args.Damage);
        switch (args.TargetPart)
        {
            case TargetBodyPart.All:
            {
                foreach (var damagePair in args.Damage.DamageDict)
                {
                    if (damagePair.Value == 0)
                        continue;

                    var damageGroup = (from @group in Proto.EnumeratePrototypes<DamageGroupPrototype>()
                        where @group.DamageTypes.Contains(damagePair.Key)
                        select @group).FirstOrDefault();

                    if (damagePair.Value < 0)
                    {
                        if (!Wound.TryGetWoundableWithMostDamage(
                                uid,
                                out var mostDamaged,
                                damageGroup?.ID))
                        {
                            actuallyInducedDamage.DamageDict[damagePair.Key] =
                                TryApplyAsphyxiationChange((uid, component), damagePair);
                            continue;
                        }

                        var damage = new DamageSpecifier();
                        damage.DamageDict.Add(damagePair.Key, damagePair.Value);

                        var beforePart = new BeforeDamageChangedEvent(damage, args.Origin);
                        RaiseLocalEvent(mostDamaged.Value, ref beforePart);

                        if (beforePart.Cancelled)
                            continue;

                        var woundHealed =
                            Wound.GetWoundsChanged(mostDamaged.Value, args.Origin, damage, component: mostDamaged.Value)
                                .DamageDict.GetValueOrDefault(damagePair.Key);

                        actuallyInducedDamage.DamageDict[damagePair.Key] = woundHealed != 0
                            ? woundHealed
                            : TryApplyAsphyxiationChange((uid, component), damagePair);
                    }
                    else
                    {
                        var bodyParts = Body.GetWoundableTargets(uid).ToList();

                        actuallyInducedDamage.DamageDict[damagePair.Key] = 0;
                        if (bodyParts.Count == 0)
                            continue;

                        var damagePerPart = new DamageSpecifier();
                        damagePerPart.DamageDict.Add(damagePair.Key, damagePair.Value / bodyParts.Count);

                        foreach (var bodyPartId in bodyParts)
                        {
                            var beforePart = new BeforeDamageChangedEvent(damagePerPart, args.Origin);
                            RaiseLocalEvent(bodyPartId, ref beforePart);

                            if (beforePart.Cancelled)
                                continue;

                            actuallyInducedDamage.DamageDict[damagePair.Key] +=
                                Wound.GetWoundsChanged(bodyPartId, args.Origin, damagePerPart).DamageDict[damagePair.Key];
                        }
                    }
                }

                break;
            }
            default:
            {
                var target = args.TargetPart ?? Body.GetRandomBodyPart(uid);
                if (args.Origin.HasValue && TryComp<TargetingComponent>(args.Origin.Value, out var targeting))
                    target = Body.GetRandomBodyPart(uid, args.Origin.Value, attackerComp: targeting);

                var (partType, symmetry) = Body.ConvertTargetBodyPart(target);
                var possibleTargets = new List<EntityUid>();
                if (Body.TryGetWoundableTargetByType(uid, partType, symmetry, out var typedTarget))
                    possibleTargets.Add(typedTarget);
                else
                    possibleTargets.AddRange(Body.GetWoundableTargets(uid));

                if (possibleTargets.Count == 0)
                {
                    actuallyInducedDamage = new DamageSpecifier();
                    break;
                }

                var chosenTarget = Random.PickAndTake(possibleTargets);

                var beforePart = new BeforeDamageChangedEvent(args.Damage, args.Origin);
                RaiseLocalEvent(chosenTarget, ref beforePart);

                if (!beforePart.Cancelled)
                    actuallyInducedDamage = Wound.GetWoundsChanged(chosenTarget, args.Origin, args.Damage);
                break;
            }
        }

        args.Damage = actuallyInducedDamage;
        args.Handled = true;
    }

    protected override void OnMobStateChanged(Entity<ConsciousnessComponent> consciousness, ref MobStateChangedEvent args)
    {
        var (uid, component) = consciousness;
        if (args.NewMobState != MobState.Dead)
            return;

        AddConsciousnessModifier(consciousness.AsNullable(), uid, -component.Cap, "DeathThreshold", ConsciousnessModType.Pain);
        // To prevent people from suddenly resurrecting while being dead. whoops

        foreach (var multiplier in
                 component.Multipliers.Where(multiplier => multiplier.Value.Type != ConsciousnessModType.Pain))
        {
            RemoveConsciousnessMultiplier(consciousness.AsNullable(), multiplier.Key.Item1, multiplier.Key.Item2);
        }

        foreach (var multiplier in
                 component.Modifiers.Where(multiplier => multiplier.Value.Type != ConsciousnessModType.Pain))
        {
            RemoveConsciousnessModifier(consciousness.AsNullable(), multiplier.Key.Item1, multiplier.Key.Item2);
        }
    }

    private bool CanPerformCpr(Entity<ConsciousnessComponent> consciousness, EntityUid user)
    {
        if (!TryComp(consciousness.Owner, out MobStateComponent? mobState))
            return false;

        if (!consciousness.Comp.NerveSystem.HasValue)
            return false;

        if (!TryGetConsciousnessModifier(
                consciousness.AsNullable(),
                consciousness.Comp.NerveSystem.Value,
                out _,
                ConsciousnessModifierIds.Asphyxiation))
            return false;

        if (MobStateSys.IsDead(consciousness, mobState))
        {
            _popup.PopupPredicted(Loc.GetString("cpr-cant-perform-dead"), consciousness, user, PopupType.Medium);
            return false;
        }

        return true;
    }

    private void OnConsciousnessInteract(Entity<ConsciousnessComponent> consciousness, ref InteractHandEvent args)
    {
        if (args.Handled)
            return;

        if (!CanPerformCpr(consciousness, args.User))
            return;

        _popup.PopupEntity(
            Loc.GetString("user-began-cpr", ("user", args.User), ("target", args.Target)),
            args.Target);

        args.Handled = _doAfter.TryStartDoAfter(new
            DoAfterArgs(EntityManager,
            args.User,
            consciousness.Comp.CprDoAfterDuration,
            new CprDoAfterEvent(),
            args.Target,
            args.Target)
        {
            NeedHand = true,
            BreakOnMove = true,
            BreakOnHandChange = true,
            CancelDuplicate = true,
            BlockDuplicate = true,
        });
    }

    private void OnCprDoAfter(Entity<ConsciousnessComponent> consciousness, ref CprDoAfterEvent args)
    {
        if (!consciousness.Comp.NerveSystem.HasValue)
            return;

        if (!CanPerformCpr(consciousness, args.User))
            return;

        if (!TryGetNerveSystem(consciousness.AsNullable(), out var nerveSys))
            return;
        var modifier = consciousness.Comp.Modifiers[(nerveSys.Value.Owner, ConsciousnessModifierIds.Asphyxiation)];

        var sex = Sex.Unsexed;
        if (TryComp<HumanoidProfileComponent>(consciousness, out var humanoid))
            sex = humanoid.Sex;

        var lungs = Body.GetBodyOrganEntityComps<LungComponent>(consciousness.Owner);
        if (_trauma.TryGetBodyTraumas(consciousness, out var traumas, TraumaType.OrganDamage))
        {
            var hearts = Body.GetBodyOrganEntityComps<HeartComponent>(consciousness.Owner);
            var cprableOrgans = new HashSet<EntityUid>();

            cprableOrgans.UnionWith(lungs.Select(lung => lung.Owner));
            cprableOrgans.UnionWith(hearts.Select(heart => heart.Owner));

            var cprableOrgansDamaged = false;
            foreach (var trauma in traumas)
            {
                if (!trauma.Comp.TraumaTarget.HasValue)
                    continue;

                if (!cprableOrgans.Contains(trauma.Comp.TraumaTarget.Value))
                    continue;

                cprableOrgansDamaged = true;
                break;
            }

            if (cprableOrgansDamaged)
            {
                foreach (var lung in lungs)
                {
                    if (!_trauma.TryChangeOrganDamageModifier(
                            lung.Owner,
                            consciousness.Comp.CprSuffocationHealAmount,
                            consciousness,
                            "FailedCPR",
                            lung.Comp2))
                    {
                        _trauma.TryAddOrganDamageModifier(lung.Owner,
                            consciousness.Comp.CprSuffocationHealAmount,
                            consciousness,
                            "FailedCPR",
                            lung.Comp2);
                    }
                }

                Pain.CleanupPainSounds(nerveSys.Value, nerveSys);
                Pain.PlayPainSound(
                    consciousness,
                    nerveSys.Value.Comp.OrganDestructionReflexSounds[sex],
                    AudioParams.Default.WithVolume(12f));

                return;
            }
        }

        var threshold = consciousness.Comp.CprSuffocationHealAmount * consciousness.Comp.CprSuffocationHealThreshold;
        if (FixedPoint2.Abs(modifier.Change) < threshold)
        {
            if (Random.Prob(_cprTraumaChance))
            {
                // Apply the damage equal to suffocation heal of CPR to all lungs.
                // I can already see people popping people's organs with fucking CPR..
                foreach (var lung in lungs)
                {
                    if (!_trauma.TryChangeOrganDamageModifier(
                            lung.Owner,
                            consciousness.Comp.CprSuffocationHealAmount,
                            consciousness,
                            "FailedCPR",
                            lung.Comp2))
                    {
                        _trauma.TryAddOrganDamageModifier(lung.Owner,
                            consciousness.Comp.CprSuffocationHealAmount,
                            consciousness,
                            "FailedCPR",
                            lung.Comp2);
                    }
                }

                Pain.CleanupPainSounds(nerveSys.Value, nerveSys);
                Pain.PlayPainSound(
                    consciousness,
                    nerveSys.Value.Comp.OrganDestructionReflexSounds[sex],
                    AudioParams.Default.WithVolume(12f));
            }
            else
            {
                Pain.CleanupPainSounds(nerveSys.Value, nerveSys);
                Pain.PlayPainSound(
                    consciousness,
                    nerveSys.Value.Comp.PainGrunts[sex],
                    AudioParams.Default.WithVolume(12f));
            }

            return;
        }

        ChangeConsciousnessModifier(
            consciousness.AsNullable(),
            nerveSys.Value,
            consciousness.Comp.CprSuffocationHealAmount,
            ConsciousnessModifierIds.Asphyxiation);

        if (consciousness.Comp.Modifiers[(nerveSys.Value, ConsciousnessModifierIds.Asphyxiation)].Change > 0)
        {
            // No fuck you
            RemoveConsciousnessModifier(consciousness.AsNullable(), nerveSys.Value, ConsciousnessModifierIds.Asphyxiation);
        }

        _popup.PopupEntity(
            Loc.GetString("user-finished-cpr-successfully", ("user", args.User), ("target", consciousness)),
            consciousness);
    }

    private void OnRejuvenate(Entity<ConsciousnessComponent> consciousness, ref RejuvenateEvent args)
    {
        var (uid, component) = consciousness;

        if (component.NerveSystem.HasValue)
        {
            foreach (var painModifier in component.NerveSystem.Value.Comp.Modifiers)
            {
                Pain.TryRemovePainModifier(
                    component.NerveSystem.Value.Owner,
                    painModifier.Key.Item1,
                    painModifier.Key.Item2,
                    component.NerveSystem.Value.Comp);
            }

            foreach (var painMultiplier in component.NerveSystem.Value.Comp.Multipliers)
            {
                Pain.TryRemovePainMultiplier(
                    component.NerveSystem.Value.Owner,
                    painMultiplier.Key,
                    component.NerveSystem.Value.Comp);
            }

            foreach (var nerve in component.NerveSystem.Value.Comp.Nerves)
            {
                foreach (var painFeelsModifier in nerve.Value.PainFeelingModifiers)
                {
                    Pain.TryRemovePainFeelsModifier(painFeelsModifier.Key.Item1, painFeelsModifier.Key.Item2, nerve.Key, nerve.Value);
                }
            }
        }

        foreach (var key in component.Multipliers
                    .Where(multiplier => multiplier.Value.Type == ConsciousnessModType.Pain)
                    .Select(multiplier => multiplier.Key)
                    .ToArray())
        {
            RemoveConsciousnessMultiplier(uid, key.Item1, key.Item2);
        }

        foreach (var key in component.Modifiers
                     .Where(modifier => modifier.Value.Type == ConsciousnessModType.Pain)
                     .Select(modifier => modifier.Key)
                     .ToArray())
        {
            RemoveConsciousnessModifier(uid, key.Item1, key.Item2);
        }

        CheckRequiredParts(consciousness);
        ForceConscious(consciousness.AsNullable(), TimeSpan.FromSeconds(5f));
    }

    private void OnGetHealableDamage(
        Entity<ConsciousnessComponent> consciousness,
        ref DamageableGetHealableDamageEvent args)
    {
        if (args.Target.Owner != consciousness.Owner)
            return;

        args.Damage = GetHealableWoundDamage(consciousness, args.Group);
        args.Handled = true;
    }

    /// <summary>
    /// Healable damage from wounds and the Asphyxiation consciousness modifier (Airloss is not stored on body-part wounds).
    /// </summary>
    private DamageSpecifier GetHealableWoundDamage(
        Entity<ConsciousnessComponent> consciousness,
        ProtoId<DamageGroupPrototype>? group)
    {
        var damage = new DamageSpecifier();
        var body = consciousness.Owner;

        if (!TryComp<BodyComponent>(body, out var bodyComp))
            return damage;

        DamageGroupPrototype? groupProto = null;
        if (group != null && !Proto.Resolve(group.Value, out groupProto))
            return damage;

        foreach (var wound in Wound.GetBodyWounds(body, bodyComp))
        {
            if (!Wound.CanHealWound(wound))
                continue;

            var severity = wound.Comp.WoundSeverityPoint;
            if (severity <= FixedPoint2.Zero)
                continue;

            var type = wound.Comp.DamageType;
            if (groupProto != null && !groupProto.DamageTypes.Contains(type))
                continue;

            damage.DamageDict.TryGetValue(type, out var existing);
            damage.DamageDict[type] = existing + severity;
        }

        if (consciousness.Comp.NerveSystem is { } nerveSys
            && consciousness.Comp.Modifiers.TryGetValue((nerveSys, ConsciousnessModifierIds.Asphyxiation), out var asphyxiationMod)
            && asphyxiationMod.Change < FixedPoint2.Zero
            && (groupProto == null || groupProto.DamageTypes.Contains(AsphyxiationDamageType)))
        {
            var asphyxiation = -asphyxiationMod.Change;
            damage.DamageDict.TryGetValue(AsphyxiationDamageType, out var existing);
            damage.DamageDict[AsphyxiationDamageType] = existing + asphyxiation;
        }

        return damage;
    }

    private void OnHandleUnhandledDamage(Entity<ConsciousnessComponent> consciousness, ref HandleUnhandledWoundsEvent args)
    {
        foreach (var damagePiece in args.UnhandledDamage.ToArray())
        {
            if (damagePiece.Key != AsphyxiationDamageType)
                continue;

            ApplyAsphyxiationChange(consciousness, damagePiece.Value);
            args.UnhandledDamage.Remove(damagePiece.Key);
        }
    }

    /// <summary>
    /// Applies Asphyxiation that cannot become a wound (consciousness modifier on the nerve system).
    /// </summary>
    /// <returns>Damage applied in the same convention as <see cref="DamageSpecifier"/> (negative = heal).</returns>
    private FixedPoint2 TryApplyAsphyxiationChange(
        Entity<ConsciousnessComponent> consciousness,
        KeyValuePair<ProtoId<DamageTypePrototype>, FixedPoint2> damagePair)
    {
        if (damagePair.Key != AsphyxiationDamageType)
            return FixedPoint2.Zero;

        return ApplyAsphyxiationChange(consciousness, damagePair.Value);
    }

    private FixedPoint2 ApplyAsphyxiationChange(
        Entity<ConsciousnessComponent> consciousness,
        FixedPoint2 damageValue)
    {
        if (!TryGetNerveSystem(consciousness.AsNullable(), out var nerveSys))
            return FixedPoint2.Zero;

        // Healing only applies when asphyxiation is already present.
        if (damageValue < 0
            && !consciousness.Comp.Modifiers.ContainsKey((nerveSys.Value, ConsciousnessModifierIds.Asphyxiation)))
        {
            return FixedPoint2.Zero;
        }

        var modifierDelta = -damageValue;

        if (!ChangeConsciousnessModifier(
                consciousness.AsNullable(),
                nerveSys.Value,
                modifierDelta,
                ConsciousnessModifierIds.Asphyxiation))
        {
            if (damageValue <= 0)
                return FixedPoint2.Zero;

            AddConsciousnessModifier(
                consciousness.AsNullable(),
                nerveSys.Value,
                modifierDelta,
                ConsciousnessModifierIds.Asphyxiation,
                ConsciousnessModType.Pain);
        }

        if (consciousness.Comp.Modifiers.TryGetValue((nerveSys.Value, ConsciousnessModifierIds.Asphyxiation), out var modifier)
            && modifier.Change > 0)
        {
            RemoveConsciousnessModifier(consciousness.AsNullable(), nerveSys.Value, ConsciousnessModifierIds.Asphyxiation);
        }

        return damageValue;
    }

    private void OnBodyPartAdded(EntityUid uid, ConsciousnessRequiredComponent component, ref BodyPartAddedEvent args)
    {
        if (args.Part.Comp.Body == null || !ConsciousnessQuery.TryComp(args.Part.Comp.Body, out var consciousness))
            return;

        consciousness.RequiredConsciousnessParts[component.Identifier] = (uid, component.CausesDeath, false);
        CheckRequiredParts((args.Part.Comp.Body.Value, consciousness));
    }

    private void OnBodyPartRemoved(EntityUid uid, ConsciousnessRequiredComponent component, ref BodyPartRemovedEvent args)
    {
        if(TerminatingOrDeleted(uid) || TerminatingOrDeleted(args.Part))
            return;

        if (args.Part.Comp.Body == null || !ConsciousnessQuery.TryComp(args.Part.Comp.Body.Value, out var consciousness))
            return;

        if (!consciousness.RequiredConsciousnessParts.TryGetValue(component.Identifier, out var value))
        {
            Log.Warning($"ConsciousnessRequirementPart with identifier {component.Identifier}:{uid} not found on body:{args.Part.Comp.Body}");
            return;
        }

        consciousness.RequiredConsciousnessParts[component.Identifier] = (uid, value.Item2, true);
        CheckRequiredParts((args.Part.Comp.Body.Value, consciousness));
    }

    private void OnOrganAdded(EntityUid uid, ConsciousnessRequiredComponent component, ref OrganAddedToBodyEvent args)
    {
        if (!ConsciousnessQuery.TryComp(args.Body, out var consciousness))
            return;

        consciousness.RequiredConsciousnessParts[component.Identifier] = (uid, component.CausesDeath, false);

        if (component.Identifier == NerveSystemIdentifier)
        {
            var nerveSys = EnsureComp<NerveSystemComponent>(uid);
            EntityUid rootNerve;
            if (HasComp<BodyPartComponent>(args.Part))
                rootNerve = args.Part;
            else if (Body.TryGetWoundableTargetByType(args.Body, BodyPartType.Head, null, out var head))
                rootNerve = head;
            else
                rootNerve = args.Part;

            nerveSys.RootNerve = rootNerve;
            consciousness.NerveSystem = (uid, nerveSys);
            Dirty(uid, nerveSys);
            Pain.RefreshNerveSystem(uid, args.Body);
        }

        CheckRequiredParts((args.Body, consciousness));
    }

    private void OnOrganRemoved(EntityUid uid, ConsciousnessRequiredComponent component, ref OrganRemovedFromBodyEvent args)
    {
        if (!ConsciousnessQuery.TryComp(args.OldBody, out var consciousness))
            return;

        if (!consciousness.RequiredConsciousnessParts.TryGetValue(component.Identifier, out var value))
        {
            Log.Warning($"ConsciousnessRequirementPart with identifier {component.Identifier}:{uid} not found on body:{args.OldBody}");
            return;
        }

        consciousness.RequiredConsciousnessParts[component.Identifier] = (uid, value.Item2, true);
        CheckRequiredParts((args.OldBody, consciousness));
    }

    private void OnConsciousnessInit(Entity<ConsciousnessComponent> uid, ref ComponentInit args)
    {
        if (uid.Comp.RawConsciousness <= 0)
        {
            uid.Comp.RawConsciousness = uid.Comp.Cap;
            Dirty(uid);
        }
    }

    private void OnConsciousnessMapInit(Entity<ConsciousnessComponent> uid, ref MapInitEvent args)
    {
        SyncConsciousnessFromMobThresholds(uid, uid.Comp);
        ApplyInitialPrototypeDamage(uid);
        CheckConscious(uid.AsNullable());
    }

    /// <summary>
    /// Prototype-set damage on <see cref="DamageableComponent"/> is initialized without raising
    /// <see cref="DamageChangedEvent"/>, so consciousness/mob-threshold systems never see it until
    /// something else damages the mob (e.g. salvage corpses spawning alive).
    /// </summary>
    /// <remarks>
    /// Do not route this through <see cref="DamageableSystem.ChangeDamage"/> — brute wound creation
    /// can dismember body parts, leaving detached entities when the mob is deleted.
    /// </remarks>
    private void ApplyInitialPrototypeDamage(EntityUid uid)
    {
        if (!TryComp<DamageableComponent>(uid, out var damageable) || _damageable.GetTotalDamage((uid, damageable)) <= 0)
            return;

        if (ConsciousnessQuery.TryComp(uid, out var consciousness))
        {
            foreach (var (type, amount) in _damageable.GetAllDamage((uid, damageable)).DamageDict)
            {
                if (amount <= 0)
                    continue;

                TryApplyAsphyxiationChange((uid, consciousness), new KeyValuePair<ProtoId<DamageTypePrototype>, FixedPoint2>(type, amount));
            }
        }

        _mobThresholds.VerifyThresholds(uid, damageable: damageable);
    }

    // start-backmen: sync mob thresholds
    private void SyncConsciousnessFromMobThresholds(EntityUid uid, ConsciousnessComponent consciousness)
    {
        if (!TryComp<MobThresholdsComponent>(uid, out var thresholds))
            return;

        if (_mobThresholds.TryGetThresholdForState(uid, MobState.Dead, out var dead, thresholds))
            consciousness.Cap = dead.Value + consciousness.CapBonus;

        if (_mobThresholds.TryGetThresholdForState(uid, MobState.Critical, out var crit, thresholds)
            || _mobThresholds.TryGetThresholdForState(uid, MobState.SoftCritical, out crit, thresholds))
        {
            consciousness.Threshold = crit.Value;
        }

        if (consciousness.RawConsciousness <= 0 || consciousness.RawConsciousness > consciousness.Cap)
            consciousness.RawConsciousness = consciousness.Cap;

        Dirty(uid, consciousness);
    }
    // end-backmen

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<ConsciousnessComponent, MetaDataComponent>();
        while (query.MoveNext(out var ent, out var consciousness, out var meta))
        {
            if (Paused(ent, meta))
                continue;

            if (consciousness.ForceDead || Timing.CurTime < consciousness.NextConsciousnessUpdate)
                continue;
            consciousness.NextConsciousnessUpdate = Timing.CurTime + consciousness.ConsciousnessUpdateTime;

            foreach (var modifier in
                     consciousness.Modifiers
                         .Where(m => m.Value.Time < Timing.CurTime)
                         .Select(m => m.Key)
                         .ToArray())
            {
                RemoveConsciousnessModifier((ent,consciousness), modifier.Item1, modifier.Item2);
            }

            foreach (var multiplier in
                     consciousness.Multipliers
                         .Where(m => m.Value.Time < Timing.CurTime)
                         .Select(m => m.Key)
                         .ToArray())
            {
                RemoveConsciousnessMultiplier((ent,consciousness), multiplier.Item1, multiplier.Item2);
            }

            if (consciousness.PassedOutTime < Timing.CurTime && consciousness.PassedOut)
            {
                consciousness.PassedOut = false;
                CheckConscious((ent, consciousness));
            }

            if (consciousness.ForceConsciousnessTime < Timing.CurTime && consciousness.ForceConscious)
            {
                consciousness.ForceConscious = false;
                CheckConscious((ent, consciousness));
            }
        }
    }

    #region Helpers

    [PublicAPI]
    public override bool CheckConscious(Entity<ConsciousnessComponent?, MobStateComponent?> target)
    {
        if (!ConsciousnessQuery.Resolve(target, ref target.Comp1, false)
            || !MobStateQuery.Resolve(target, ref target.Comp2, false))
            return false;

        var shouldBeConscious =
            target.Comp1.Consciousness > target.Comp1.Threshold || target.Comp1 is { ForceUnconscious: false, ForceConscious: true };

        var ev = new ConsciousUpdateEvent(target.Comp1, shouldBeConscious);
        RaiseLocalEvent(target, ref ev);

        SetConscious(target, shouldBeConscious);
        UpdateMobState(target);

        return shouldBeConscious;
    }

    [PublicAPI]
    public override void ForcePassOut(
        Entity<ConsciousnessComponent?> target,
        TimeSpan time)
    {
        if (!ConsciousnessQuery.Resolve(target, ref target.Comp, false))
            return;

        target.Comp.PassedOutTime = Timing.CurTime + time;
        target.Comp.PassedOut = true;

        CheckConscious(target);
    }

    [PublicAPI]
    public override void ForceConscious(
        Entity<ConsciousnessComponent?> target,
        TimeSpan time)
    {
        if (!ConsciousnessQuery.Resolve(target, ref target.Comp, false))
            return;

        target.Comp.ForceConsciousnessTime = Timing.CurTime + time;
        target.Comp.ForceConscious = true;

        CheckConscious(target);
    }

    [PublicAPI]
    public override void ClearForceEffects(
        Entity<ConsciousnessComponent?> target)
    {
        if (!ConsciousnessQuery.Resolve(target, ref target.Comp, false))
            return;

        target.Comp.ForceConscious = false;
        target.Comp.PassedOut = false;

        CheckConscious(target);
    }

    #endregion

    #region Modifiers and Multipliers

    [PublicAPI]
    public override bool AddConsciousnessModifier(Entity<ConsciousnessComponent?> target,
        EntityUid modifierOwner,
        FixedPoint2 modifier,
        string identifier = "Unspecified",
        ConsciousnessModType type = ConsciousnessModType.Generic,
        TimeSpan? time = null)
    {
        if (!ConsciousnessQuery.Resolve(target, ref target.Comp, false))
            return false;

        if (!target.Comp.Modifiers.TryAdd((modifierOwner, identifier),
                new ConsciousnessModifier(modifier, time.HasValue ? Timing.CurTime + time :  time, type)))
            return false;

        UpdateConsciousnessModifiers(target);

        return true;
    }

    [PublicAPI]
    public override bool RemoveConsciousnessModifier(Entity<ConsciousnessComponent?> target,
        EntityUid modifierOwner,
        string identifier)
    {
        if (!ConsciousnessQuery.Resolve(target, ref target.Comp, false))
            return false;

        if (!target.Comp.Modifiers.Remove((modifierOwner, identifier)))
            return false;

        UpdateConsciousnessModifiers(target);

        return true;
    }

    [PublicAPI]
    public override bool SetConsciousnessModifier(Entity<ConsciousnessComponent?> target,
        EntityUid modifierOwner,
        FixedPoint2 modifierChange,
        string identifier = "Unspecified",
        ConsciousnessModType type = ConsciousnessModType.Generic,
        TimeSpan? time = null)
    {
        if (!ConsciousnessQuery.Resolve(target, ref target.Comp, false))
            return false;

        var newModifier = new ConsciousnessModifier(Change: modifierChange, Time: time.HasValue ? Timing.CurTime + time : time, Type: type);
        target.Comp.Modifiers[(modifierOwner, identifier)] = newModifier;

        UpdateConsciousnessModifiers(target);

        return true;
    }

    [PublicAPI]
    public override bool ChangeConsciousnessModifier(Entity<ConsciousnessComponent?> target,
        EntityUid modifierOwner,
        FixedPoint2 modifierChange,
        string identifier,
        TimeSpan? time = null)
    {
        if (!ConsciousnessQuery.Resolve(target, ref target.Comp, false) ||
            !target.Comp.Modifiers.TryGetValue((modifierOwner, identifier), out var oldModifier))
            return false;

        var newModifier =
            oldModifier with {Change = oldModifier.Change + modifierChange, Time = time.HasValue ? Timing.CurTime + time :  time};

        target.Comp.Modifiers[(modifierOwner, identifier)] = newModifier;

        UpdateConsciousnessModifiers(target);

        return true;
    }

    [PublicAPI]
    public override bool AddConsciousnessMultiplier(Entity<ConsciousnessComponent?> target,
        EntityUid multiplierOwner,
        FixedPoint2 multiplier,
        string identifier = "Unspecified",
        ConsciousnessModType type = ConsciousnessModType.Generic,
        TimeSpan? time = null)
    {
        if (!ConsciousnessQuery.Resolve(target, ref target.Comp, false))
            return false;

        if (!target.Comp.Multipliers.TryAdd((multiplierOwner, identifier),
                new ConsciousnessMultiplier(multiplier, time.HasValue ? Timing.CurTime + time :  time, type)))
            return false;

        UpdateConsciousnessMultipliers(target);
        UpdateConsciousnessModifiers(target);

        return true;
    }

    [PublicAPI]
    public override bool RemoveConsciousnessMultiplier(Entity<ConsciousnessComponent?> target,
        EntityUid multiplierOwner,
        string identifier)
    {
        if (!ConsciousnessQuery.Resolve(target, ref target.Comp, false))
            return false;

        if (!target.Comp.Multipliers.Remove((multiplierOwner, identifier)))
            return false;

        UpdateConsciousnessMultipliers(target);
        UpdateConsciousnessModifiers(target);

        return true;
    }

    #endregion

    #region Pain Helpers

    /// <summary>
    /// Gets the total pain level from the entity's nerve system, if available.
    /// </summary>
    /// <param name="target">Target entity with consciousness component</param>
    /// <returns>Total pain level as float, or null if not available</returns>
    [PublicAPI]
    public float? GetTotalPain(Entity<ConsciousnessComponent?> target)
    {
        if (!TryGetNerveSystem(target, out var nerveSys))
            return null;

        return (float)nerveSys.Value.Comp.Pain;
    }

    /// <summary>
    /// Gets the pain causes from the entity's nerve system modifiers and consciousness modifiers.
    /// </summary>
    /// <param name="target">Target entity</param>
    /// <returns>Dictionary with pain causes (identifier -> value), or null if not available</returns>
    [PublicAPI]
    public Dictionary<string, float>? GetPainCauses(Entity<ConsciousnessComponent?> target)
    {
        // Start-backmen: get pain causes from both NerveSystemComponent and ConsciousnessComponent
        if (!ConsciousnessQuery.Resolve(target, ref target.Comp, false))
            return null;

        var painCauses = new Dictionary<string, float>();

        // Get pain modifiers from nerve system (physical pain from wounds)
        if (TryGetNerveSystem(target, out var nerveSys))
        {
            foreach (var ((nerveUid, identifier), modifier) in nerveSys.Value.Comp.Modifiers)
            {
                // Apply modifiers to get actual pain value (with multipliers)
                var actualPain = _pain.ApplyModifiersToPain(
                    nerveUid,
                    modifier.Change,
                    nerveSys.Value.Comp,
                    modifier.PainType);

                if (actualPain > 0)
                {
                    if (painCauses.TryGetValue(identifier, out var existingValue))
                    {
                        painCauses[identifier] = existingValue + (float)actualPain;
                    }
                    else
                    {
                        painCauses[identifier] = (float)actualPain;
                    }
                }
            }
        }

        // Get pain modifiers from consciousness component (other pain causes like Suffocation, Bloodloss, etc.)
        foreach (var ((modifierOwner, identifier), modifier) in target.Comp.Modifiers)
        {
            if (modifier.Type != ConsciousnessModType.Pain)
                continue;

            // Only include negative modifiers (they reduce consciousness, which is what we want to show as pain)
            if (modifier.Change < 0)
            {
                var painValue = (float)FixedPoint2.Abs(modifier.Change);
                if (painCauses.TryGetValue(identifier, out var existingValue))
                {
                    painCauses[identifier] = existingValue + painValue;
                }
                else
                {
                    painCauses[identifier] = painValue;
                }
            }
        }

        return painCauses.Count > 0 ? painCauses : null;
        // End-backmen: get pain causes from both NerveSystemComponent and ConsciousnessComponent
    }

    #endregion
}
