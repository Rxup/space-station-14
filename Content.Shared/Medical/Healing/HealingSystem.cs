using Content.Shared.Administration.Logs;
using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Backmen.Body.Systems; // backmen: body
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Database;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.IdentityManagement;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Stacks;
using Robust.Shared.Audio.Systems;
using System.Linq;
using Content.Shared.Backmen.Damage;
using Content.Shared.Backmen.Medical;
using Content.Shared.Backmen.Surgery.Consciousness.Components;
using Content.Shared.Backmen.Surgery.Traumas.Components;
using Content.Shared.Backmen.Surgery.Traumas.Systems;
using Content.Shared.Backmen.Surgery.Wounds.Components;
using Content.Shared.Backmen.Surgery.Wounds.Systems;
using Content.Shared.Backmen.Targeting;
using Robust.Shared.Audio;
using Robust.Shared.GameObjects;

namespace Content.Shared.Medical.Healing;

public sealed partial class HealingSystem : EntitySystem
{
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private ISharedAdminLogManager _adminLogger = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private SharedBloodstreamSystem _bloodstreamSystem = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedStackSystem _stacks = default!;
    [Dependency] private SharedInteractionSystem _interactionSystem = default!;
    [Dependency] private MobThresholdSystem _mobThresholdSystem = default!;
    [Dependency] private BkmBodySharedSystem _bodySystem = default!; // backmen: body
    [Dependency] private SharedPopupSystem _popupSystem = default!;
    [Dependency] private SharedSolutionContainerSystem _solutionContainerSystem = default!;

    // start-backmen: healing-fallback
    [Dependency] private WoundSystem _wounds = default!;
    [Dependency] private TraumaSystem _trauma = default!;
    [Dependency] private BackmenDamageModelSystem _backmenDamageModel = default!;
    [Dependency] private BackmenMedicalTargetSystem _medicalTarget = default!;
    // end-backmen: healing-fallback

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HealingComponent, UseInHandEvent>(OnHealingUse);
        SubscribeLocalEvent<HealingComponent, AfterInteractEvent>(OnHealingAfterInteract);
        SubscribeLocalEvent<DamageableComponent, HealingDoAfterEvent>(OnDoAfter);
        SubscribeLocalEvent<BodyComponent, HealingDoAfterEvent>(OnBodyDoAfter); // backmen: healing-fallback
    }

    private void OnDoAfter(Entity<DamageableComponent> target, ref HealingDoAfterEvent args)
    {
        var dontRepeat = false;

        // Consciousness check because some body entities don't have Consciousness; Backmen
        if (!TryComp(args.Used, out HealingComponent? healing)
            || HasComp<BodyComponent>(target) && HasComp<ConsciousnessComponent>(target))
            return;

        if (args.Handled || args.Cancelled)
            return;

        // start-backmen: damage-container
        if (!_backmenDamageModel.TryGetDamageContainer(target, out var damageContainer))
            return;

        if (healing.DamageContainers is not null &&
            damageContainer is not null &&
            !healing.DamageContainers.Contains(damageContainer.Value))
        {
            return;
        }
        // end-backmen: damage-container

        TryComp<BloodstreamComponent>(target, out var bloodstream);

        // Heal some bloodloss damage.
        if (healing.BloodlossModifier != 0 && bloodstream != null)
        {
            var isBleeding = bloodstream.BleedAmount > 0;
            _bloodstreamSystem.TryModifyBleedAmount((target.Owner, bloodstream), healing.BloodlossModifier);
            if (isBleeding != bloodstream.BleedAmount > 0)
            {
                var popup = (args.User == target.Owner)
                    ? Loc.GetString("medical-item-stop-bleeding-self")
                    : Loc.GetString("medical-item-stop-bleeding", ("target", Identity.Entity(target.Owner, EntityManager)));
                _popupSystem.PopupClient(popup, target, args.User);
            }
        }

        // Restores missing blood
        if (healing.ModifyBloodLevel != 0 && bloodstream != null)
            _bloodstreamSystem.TryModifyBloodLevel((target.Owner, bloodstream), healing.ModifyBloodLevel);

        if (!_damageable.TryChangeDamage(target.Owner, healing.Damage * _damageable.UniversalTopicalsHealModifier, out var healed, true, origin: args.Args.User) && healing.BloodlossModifier != 0)
            return;

        var total = healed.GetTotal();

        // Re-verify that we can heal the damage.
        if (TryComp<StackComponent>(args.Used.Value, out var stackComp))
        {
            _stacks.ReduceCount((args.Used.Value, stackComp), 1);

            if (_stacks.GetCount((args.Used.Value, stackComp)) <= 0)
                dontRepeat = true;
        }
        else
        {
            PredictedQueueDel(args.Used.Value);
        }

        if (target.Owner != args.User)
        {
            _adminLogger.Add(LogType.Healed,
                $"{ToPrettyString(args.User):user} healed {ToPrettyString(target.Owner):target} for {total:damage} damage");
        }
        else
        {
            _adminLogger.Add(LogType.Healed,
                $"{ToPrettyString(args.User):user} healed themselves for {total:damage} damage");
        }

        _audio.PlayPredicted(healing.HealingEndSound, target.Owner, args.User);

        // Logic to determine the whether or not to repeat the healing action
        args.Repeat = HasDamage((args.Used.Value, healing), target) && !dontRepeat;
        args.Handled = true;

        if (!args.Repeat)
        {
            _popupSystem.PopupClient(Loc.GetString("medical-item-finished-using", ("item", args.Used)), target.Owner, args.User);
            return;
        }

        // Update our self heal delay so it shortens as we heal more damage.
        if (args.User == target.Owner)
            args.Args.Delay = healing.Delay * GetScaledHealingPenalty(target.Owner, healing.SelfHealPenaltyMultiplier);
    }

    // start-backmen: healing-fallback
    private void OnBodyDoAfter(EntityUid ent, BodyComponent comp, ref HealingDoAfterEvent args)
    {
        if (args.Target == null || !TryComp(args.Used, out HealingComponent? healing))
            return;

        if (args.Handled || args.Cancelled)
            return;

        EntityUid targetedWoundable;
        Dictionary<string, FixedPoint2> stuffToHeal;

        if (TryGetEntity(args.TargetWoundable, out var resolvedWoundable) && resolvedWoundable != null)
        {
            targetedWoundable = resolvedWoundable.Value;
            stuffToHeal = healing.Damage.DamageDict
                .Where(damage => _wounds.HasDamageOfType(targetedWoundable, damage.Key))
                .ToDictionary(damage => damage.Key.Id, damage => damage.Value);
        }
        else if (!_medicalTarget.TryResolveHealTarget(ent, args.User, healing, out targetedWoundable, out stuffToHeal, out _))
        {
            args.Handled = true;
            return;
        }

        if (!TryComp<WoundableComponent>(targetedWoundable, out var woundableComp))
        {
            args.Handled = true;
            return;
        }

        var totalBleeds = _medicalTarget.GetTotalBleeds(targetedWoundable, woundableComp);

        var woundableDamageContainer = woundableComp.DamageContainer;
        if (healing.DamageContainers != null && woundableDamageContainer.HasValue &&
            !healing.DamageContainers.Contains(woundableDamageContainer.Value))
        {
            _popupSystem.PopupEntity(
                Loc.GetString("cant-heal-damage-rebell", ("target", ent), ("used", args.Used)),
                ent,
                args.User,
                PopupType.Medium);
            args.Handled = true;
            return;
        }

        var dontRepeat = false;
        var bleedsManipulated = false;

        var bleedStopAbility = FixedPoint2.New(-healing.BloodlossModifier);
        if (totalBleeds > healing.UnableToHealBleedsThreshold
            && (healing.BloodlossModifier != 0 || stuffToHeal.Count == 0))
        {
            _popupSystem.PopupEntity(
                Loc.GetString("medical-item-cant-use-bleeding-heavy", ("target", ent)),
                ent,
                args.User,
                PopupType.MediumCaution);
            args.Handled = true;
            return;
        }

        if (healing.BloodlossModifier != 0)
        {
            foreach (var wound in _wounds.GetWoundableWoundsWithComp<BleedInflicterComponent>(targetedWoundable, woundableComp))
            {
                var bleeds = wound.Comp2;
                if (!bleeds.IsBleeding)
                    continue;

                if (bleedStopAbility > bleeds.BleedingAmount)
                {
                    bleedStopAbility -= bleeds.BleedingAmountRaw;

                    bleeds.BleedingAmountRaw = 0;
                    bleeds.Scaling = 0;

                    bleeds.IsBleeding = false;
                }
                else
                {
                    bleeds.BleedingAmountRaw -= bleedStopAbility;
                }
            }
        }

        var isBleeding = -healing.BloodlossModifier != bleedStopAbility;
        if (!isBleeding)
        {
            _wounds.TryHaltAllBleeding(targetedWoundable, woundableComp);
            if (healing.ModifyBloodLevel != 0)
            {
                _bloodstreamSystem.TryModifyBloodLevel(ent, healing.ModifyBloodLevel);
                bleedsManipulated = true;
            }
        }

        var healedTotal = FixedPoint2.Zero;
        foreach (var (key, value) in stuffToHeal)
        {
            if (!_wounds.TryHealWoundsOnWoundable(targetedWoundable, -value, key, out var healed, woundableComp))
                continue;

            healedTotal += healed;
        }

        if (healedTotal <= 0 && !bleedsManipulated)
        {
            string popup;
            if (_trauma.AnyTraumasBlockingHealing(targetedWoundable, woundableComp))
                popup = Loc.GetString("medical-item-requires-surgery-rebell", ("target", args.Target));
            else if (totalBleeds > healing.UnableToHealBleedsThreshold && healing.BloodlossModifier != 0)
                popup = Loc.GetString("medical-item-cant-use-bleeding-heavy", ("target", args.Target));
            else
                popup = Loc.GetString("medical-item-no-healable-damage", ("target", args.Target));

            _popupSystem.PopupEntity(
                popup,
                args.Target.Value,
                args.User,
                PopupType.MediumCaution);
            args.Handled = true;
            return;
        }

        if (TryComp<StackComponent>(args.Used.Value, out var stackComp))
        {
            if(!_stacks.TryUse((args.Used.Value,stackComp), 1))
            {
                args.Handled = true;
                return;
            }

            if (_stacks.GetCount((args.Used.Value, stackComp)) <= 0)
                dontRepeat = true;
        }
        else
        {
            QueueDel(args.Used.Value);
        }

        if (ent != args.User)
        {
            _adminLogger.Add(LogType.Healed,
                $"{ToPrettyString(args.User):user} healed {ToPrettyString(ent):target} for {healedTotal:damage} damage");
        }
        else
        {
            _adminLogger.Add(LogType.Healed,
                $"{ToPrettyString(args.User):user} healed themselves for {healedTotal:damage} damage");
        }

        _audio.PlayPvs(healing.HealingEndSound, ent, AudioParams.Default.WithVariation(0.125f).WithVolume(1f));

        args.Repeat = IsBodyDamaged((ent, comp), args.User, (args.Used.Value, healing), false) && !dontRepeat;
        args.Handled = true;

        if (args.Repeat)
            return;

        if (_trauma.AnyTraumasBlockingHealing(targetedWoundable, woundableComp))
        {
            _popupSystem.PopupEntity(Loc.GetString("medical-item-requires-partial-surgery-rebell", ("target", ent)), ent, args.User, PopupType.MediumCaution);
            return;
        }

        _popupSystem.PopupEntity(Loc.GetString("medical-item-finished-using", ("item", args.Used)), ent, args.User, PopupType.Medium);
    }
    // end-backmen: healing-fallback

    private bool HasDamage(Entity<HealingComponent> healing, Entity<DamageableComponent> target)
    {
        var damageableDict = _damageable.GetAllDamage(target.AsNullable()).DamageDict;
        var healingDict = healing.Comp.Damage.DamageDict;
        foreach (var type in healingDict)
        {
            if (damageableDict.TryGetValue(type.Key, out var amount) && amount > 0)
            {
                return true;
            }
        }

        if (TryComp<BloodstreamComponent>(target, out var bloodstream))
        {
            // Is ent missing blood that we can restore?
            if (healing.Comp.ModifyBloodLevel > 0
                && _solutionContainerSystem.ResolveSolution(target.Owner, bloodstream.BloodSolutionName, ref bloodstream.BloodSolution, out var bloodSolution)
                && _bloodstreamSystem.GetBloodLevel((target, bloodstream)) < 1)
            {
                return true;
            }

            // Is ent bleeding and can we stop it?
            if (healing.Comp.BloodlossModifier < 0 && bloodstream.BleedAmount > 0)
            {
                return true;
            }
        }

        return false;
    }

    // start-backmen: healing-fallback
    private bool IsBodyDamaged(Entity<BodyComponent> target, EntityUid user, Entity<HealingComponent> healing, bool throwPopups = true)
    {
        if (!HasComp<ConsciousnessComponent>(target))
            return false;

        if (_medicalTarget.TryResolveHealTarget(target, user, healing.Comp, out _, out _, out _))
            return true;

        if (!throwPopups)
            return false;

        if (!TryComp<TargetingComponent>(user, out var targeting))
            return false;

        var (partType, symmetry) = _bodySystem.ConvertTargetBodyPart(targeting.Target);
        if (!_bodySystem.TryGetWoundableTargetByType(target, partType, symmetry, out var targetedWoundable))
        {
            _popupSystem.PopupEntity(Loc.GetString("does-not-exist-rebell"), target, user, PopupType.MediumCaution);
            return false;
        }

        if (!TryComp<WoundableComponent>(targetedWoundable, out var woundableComp))
            return false;

        var totalBleeds = _medicalTarget.GetTotalBleeds(targetedWoundable, woundableComp);
        var stuffToHeal = healing.Comp.Damage.DamageDict
            .Where(damage => _wounds.HasDamageOfType(targetedWoundable, damage.Key))
            .ToDictionary(damage => damage.Key, damage => damage.Value);

        if (totalBleeds > healing.Comp.UnableToHealBleedsThreshold
            && (healing.Comp.BloodlossModifier != 0 || stuffToHeal.Count == 0))
        {
            _popupSystem.PopupEntity(
                Loc.GetString("medical-item-cant-use-bleeding-heavy", ("target", target)),
                target,
                user,
                PopupType.MediumCaution);
        }
        else if (_trauma.AnyTraumasBlockingHealing(targetedWoundable, woundableComp))
        {
            _popupSystem.PopupEntity(
                Loc.GetString("medical-item-requires-surgery-rebell", ("target", target)),
                target,
                user,
                PopupType.MediumCaution);
        }
        else
        {
            _popupSystem.PopupEntity(
                Loc.GetString("medical-item-no-healable-damage", ("target", target)),
                target,
                user,
                PopupType.MediumCaution);
        }

        return false;
    }
    // end-backmen: healing-fallback

    private void OnHealingUse(Entity<HealingComponent> healing, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        if (TryHeal(healing, args.User, args.User))
            args.Handled = true;
    }

    private void OnHealingAfterInteract(Entity<HealingComponent> healing, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target == null)
            return;

        if (TryHeal(healing, args.Target.Value, args.User))
            args.Handled = true;
    }

    private bool TryHeal(Entity<HealingComponent> healing, Entity<DamageableComponent?> target, EntityUid user)
    {
        if (!Resolve(target, ref target.Comp, false))
            return false;

        // start-backmen: damage-container
        if (!_backmenDamageModel.TryGetDamageContainer(target, out var damageContainer))
            return false;

        if (healing.Comp.DamageContainers is not null &&
            damageContainer is not null &&
            !healing.Comp.DamageContainers.Contains(damageContainer.Value))
        {
            return false;
        }
        // end-backmen: damage-container

        if (user != target.Owner && !_interactionSystem.InRangeUnobstructed(user, target.Owner, popup: true))
            return false;

        if (TryComp<StackComponent>(healing, out var stack) && stack.Count < 1)
            return false;

        var anythingToDo =
            HasDamage(healing, target!) ||
            (TryComp<BodyComponent>(target, out var bodyComp) &&
             IsBodyDamaged((target, bodyComp), user, healing)) ||
            healing.Comp.ModifyBloodLevel > 0 // Special case if healing item can restore lost blood...
                && TryComp<BloodstreamComponent>(target, out var bloodstream)
                && _solutionContainerSystem.ResolveSolution(target.Owner, bloodstream.BloodSolutionName, ref bloodstream.BloodSolution, out var bloodSolution)
                && bloodSolution.Volume < bloodSolution.MaxVolume; // ...and there is lost blood to restore.

        if (!anythingToDo)
        {
            _popupSystem.PopupClient(Loc.GetString("medical-item-cant-use", ("item", healing.Owner)), healing, user);
            return false;
        }

        _audio.PlayPredicted(healing.Comp.HealingBeginSound, healing, user);

        var isNotSelf = user != target.Owner;

        if (isNotSelf)
        {
            var msg = Loc.GetString("medical-item-popup-target", ("user", Identity.Entity(user, EntityManager)), ("item", healing.Owner));
            _popupSystem.PopupEntity(msg, target, target, PopupType.Medium);
        }

        var delay = isNotSelf
            ? healing.Comp.Delay
            : healing.Comp.Delay * GetScaledHealingPenalty(target, healing.Comp.SelfHealPenaltyMultiplier);

        // start-backmen: medical-targeting
        var healingEvent = new HealingDoAfterEvent();
        if (TryComp<BodyComponent>(target, out _)
            && HasComp<ConsciousnessComponent>(target)
            && _medicalTarget.TryResolveHealTarget(target, user, healing.Comp, out var woundable, out _, out _))
        {
            healingEvent.TargetWoundable = GetNetEntity(woundable);
        }
        // end-backmen: medical-targeting

        var doAfterEventArgs =
            new DoAfterArgs(EntityManager, user, delay, healingEvent, target, target: target, used: healing)
            {
                // Didn't break on damage as they may be trying to prevent it and
                // not being able to heal your own ticking damage would be frustrating.
                NeedHand = true,
                BreakOnMove = true,
                BreakOnWeightlessMove = false,
            };

        _doAfter.TryStartDoAfter(doAfterEventArgs);
        return true;
    }

    /// <summary>
    /// Scales the self-heal penalty based on the amount of damage taken
    /// </summary>
    /// <param name="ent">Entity we're healing</param>
    /// <param name="mod">Maximum modifier we can have.</param>
    /// <returns>Modifier we multiply our healing time by</returns>
    public float GetScaledHealingPenalty(Entity<DamageableComponent?, MobThresholdsComponent?> ent, float mod)
    {
        if (!Resolve(ent, ref ent.Comp1, ref ent.Comp2, false))
            return mod;

        if (!_mobThresholdSystem.TryGetThresholdForState(ent, MobState.Critical, out var amount, ent.Comp2))
            return 1;

        var percentDamage = (float)(_damageable.GetTotalDamage(ent.AsNullable()) / amount);

        if (TryComp<ConsciousnessComponent>(ent, out var consciousness))
        {
            percentDamage = (float)(consciousness.Threshold / (consciousness.Cap - consciousness.Consciousness)); // backmen edit; consciousness
        }
        //basically make it scale from 1 to the multiplier.

        var output = percentDamage * (mod - 1) + 1;
        return Math.Max(output, 1);
    }
}
