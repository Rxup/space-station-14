using Content.Shared.Administration.Logs;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Damage;
using Content.Shared.Database;
using Content.Shared.DoAfter;
using Content.Shared.Body.Components;
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
using Content.Shared.Backmen.Surgery.Consciousness.Components;
using Content.Shared.Backmen.Surgery.Traumas.Components;
using Content.Shared.Backmen.Surgery.Traumas.Systems;
using Content.Shared.Backmen.Surgery.Wounds.Components;
using Content.Shared.Backmen.Surgery.Wounds.Systems;
using Content.Shared.Backmen.Targeting;
using Robust.Shared.Audio;
using Robust.Shared.Utility;

namespace Content.Shared.Medical.Healing;

public sealed class HealingSystem : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly ISharedAdminLogManager _adminLogger = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly SharedBloodstreamSystem _bloodstreamSystem = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedStackSystem _stacks = default!;
    [Dependency] private readonly SharedInteractionSystem _interactionSystem = default!;
    [Dependency] private readonly MobThresholdSystem _mobThresholdSystem = default!;
    [Dependency] private readonly SharedBodySystem _bodySystem = default!;
    [Dependency] private readonly SharedPopupSystem _popupSystem = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solutionContainerSystem = default!;

    // backmen edit start
    [Dependency] private readonly WoundSystem _wounds = default!;
    [Dependency] private readonly TraumaSystem _trauma = default!;
    // backmen edit end

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HealingComponent, UseInHandEvent>(OnHealingUse);
        SubscribeLocalEvent<HealingComponent, AfterInteractEvent>(OnHealingAfterInteract);
        SubscribeLocalEvent<DamageableComponent, HealingDoAfterEvent>(OnDoAfter);
        SubscribeLocalEvent<BodyComponent, HealingDoAfterEvent>(OnBodyDoAfter); // backmen edit
    }

    private void OnDoAfter(Entity<DamageableComponent> target, ref HealingDoAfterEvent args)
    {
        var dontRepeat = false;

        // Consciousness check because some body entities don't have Consciousness; Backmen
        if (!TryComp(args.Used, out HealingComponent? healing) || HasComp<BodyComponent>(target) && HasComp<ConsciousnessComponent>(target))
            return;

        if (args.Handled || args.Cancelled)
            return;

        if (healing.DamageContainers is not null &&
            target.Comp.DamageContainerID is not null &&
            !healing.DamageContainers.Contains(target.Comp.DamageContainerID.Value))
        {
            return;
        }

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

        var healed = _damageable.TryChangeDamage(target.Owner, healing.Damage * _damageable.UniversalTopicalsHealModifier, true, origin: args.Args.User);

        if (healed == null && healing.BloodlossModifier != 0)
            return;

        var total = healed?.GetTotal() ?? FixedPoint2.Zero;

        // Re-verify that we can heal the damage.
        if (TryComp<StackComponent>(args.Used.Value, out var stackComp))
        {
            _stacks.Use(args.Used.Value, 1, stackComp);

            if (_stacks.GetCount(args.Used.Value, stackComp) <= 0)
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
        if (!args.Repeat && !dontRepeat)
            _popupSystem.PopupClient(Loc.GetString("medical-item-finished-using", ("item", args.Used)), target.Owner, args.User);
        args.Handled = true;
    }

    // backmen edit start
    private void OnBodyDoAfter(EntityUid ent, BodyComponent comp, ref HealingDoAfterEvent args)
    {
        if (args.Target == null || !TryComp(args.Used, out HealingComponent? healing))
            return;

        if (args.Handled || args.Cancelled)
            return;

        var stuffToHeal = new Dictionary<string, FixedPoint2>();
        var targetedWoundable = EntityUid.Invalid;
        if (TryComp<TargetingComponent>(args.User, out var targeting))
        {
            var (partType, symmetry) = _bodySystem.ConvertTargetBodyPart(targeting.Target);
            var targetedBodyPart = _bodySystem.GetBodyChildrenOfType(ent, partType, comp, symmetry).ToList().FirstOrDefault();

            foreach (var damage in
                     healing.Damage.DamageDict.Where(damage => _wounds.HasDamageOfType(targetedBodyPart.Id, damage.Key)))
            {
                stuffToHeal.Add(damage.Key, damage.Value);
            }

            targetedWoundable = targetedBodyPart.Id;
        }

        if (!TryComp<WoundableComponent>(targetedWoundable, out var woundableComp))
            return;

        var totalBleeds = FixedPoint2.Zero;
        foreach (var wound in
                 _wounds.GetWoundableWoundsWithComp<BleedInflicterComponent>(targetedWoundable, woundableComp))
        {
            if (!wound.Comp2.IsBleeding)
                continue;

            totalBleeds += wound.Comp2.BleedingAmountRaw;
        }

        var woundableDamageContainer = woundableComp.DamageContainerID;
        if (healing.DamageContainers != null && woundableDamageContainer.HasValue &&
            !healing.DamageContainers.Contains(woundableDamageContainer.Value))
        {
            _popupSystem.PopupEntity(
                Loc.GetString("cant-heal-damage-rebell", ("target", ent), ("used", args.Used)),
                ent,
                args.User,
                PopupType.Medium);
            return;
        }

        var dontRepeat = false;
        var bleedsManipulated = false;

        var bleedStopAbility = FixedPoint2.New(-healing.BloodlossModifier);
        if (totalBleeds > healing.UnableToHealBleedsThreshold)
        {
            _popupSystem.PopupEntity(
                Loc.GetString("medical-item-cant-use-rebell", ("target", ent)),
                ent,
                args.User,
                PopupType.MediumCaution);
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
            if (bleedStopAbility != 0 && bleedStopAbility != -healing.BloodlossModifier)
            {
                _popupSystem.PopupEntity(
                    Loc.GetString("rebell-medical-item-stop-bleeding-fully"),
                    ent,
                    args.User);
            }

            _wounds.TryHaltAllBleeding(targetedWoundable, woundableComp);
            if (healing.ModifyBloodLevel != 0)
            {
                _bloodstreamSystem.TryModifyBloodLevel(ent, healing.ModifyBloodLevel);
                bleedsManipulated = true;
            }
        }
        else
        {
            _popupSystem.PopupEntity(
                Loc.GetString("rebell-medical-item-stop-bleeding-partially"),
                ent,
                args.User);
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
            _popupSystem.PopupEntity(
                _trauma.AnyTraumasBlockingHealing(targetedWoundable, woundableComp)
                    ? Loc.GetString("medical-item-requires-surgery-rebell", ("target", args.Target))
                    : Loc.GetString("medical-item-cant-use-rebell", ("target", args.Target)),
                args.Target.Value,
                args.User,
                PopupType.MediumCaution);
            return;
        }

        // Re-verify that we can heal the damage.
        if (TryComp<StackComponent>(args.Used.Value, out var stackComp))
        {
            _stacks.Use(args.Used.Value, 1, stackComp);

            if (_stacks.GetCount(args.Used.Value, stackComp) <= 0)
                dontRepeat = true;
        }
        else
        {
            QueueDel(args.Used.Value);
        }

        if (ent != args.User)
        {
            _adminLogger.Add(LogType.Healed,
                $"{EntityManager.ToPrettyString(args.User):user} healed {EntityManager.ToPrettyString(ent):target} for {healedTotal:damage} damage");
        }
        else
        {
            _adminLogger.Add(LogType.Healed,
                $"{EntityManager.ToPrettyString(args.User):user} healed themselves for {healedTotal:damage} damage");
        }

        _audio.PlayPvs(healing.HealingEndSound, ent, AudioParams.Default.WithVariation(0.125f).WithVolume(1f));

        // Logic to determine whether or not to repeat the healing action
        args.Repeat = IsBodyDamaged((ent, comp), args.User, (args.Used.Value, healing), false) && !dontRepeat;
        args.Handled = true;

        if (args.Repeat)
            return;

        if (_trauma.AnyTraumasBlockingHealing(targetedWoundable, woundableComp))
        {
            _popupSystem.PopupEntity(Loc.GetString("medical-item-requires-partial-surgery-rebell", ("target", ent)), ent, args.User, PopupType.MediumCaution);
            return;
        }

        if (bleedStopAbility != -healing.BloodlossModifier)
            _popupSystem.PopupEntity(Loc.GetString("medical-item-finished-using", ("item", args.Used)), ent, args.User, PopupType.Medium);

    }
    // backmen edit end

    private bool HasDamage(Entity<HealingComponent> healing, Entity<DamageableComponent> target)
    {
        var damageableDict = target.Comp.Damage.DamageDict;
        var healingDict = healing.Comp.Damage.DamageDict;
        foreach (var type in healingDict)
        {
            if (damageableDict[type.Key].Value > 0)
            {
                return true;
            }
        }

        if (TryComp<BloodstreamComponent>(target, out var bloodstream))
        {
            // Is ent missing blood that we can restore?
            if (healing.Comp.ModifyBloodLevel > 0
                && _solutionContainerSystem.ResolveSolution(target.Owner, bloodstream.BloodSolutionName, ref bloodstream.BloodSolution, out var bloodSolution)
                && bloodSolution.Volume < bloodSolution.MaxVolume)
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

    // backmen edit start
    private bool IsBodyDamaged(Entity<BodyComponent> target, EntityUid user, Entity<HealingComponent> healing, bool throwPopups = true)
    {
        if (!HasComp<ConsciousnessComponent>(target))
            return false;

        if (!TryComp<TargetingComponent>(user, out var targeting))
            return false;

        var (partType, symmetry) = _bodySystem.ConvertTargetBodyPart(targeting.Target);
        var targetedBodyPart = _bodySystem.GetBodyChildrenOfType(target, partType, target, symmetry).ToList().FirstOrNull();

        if (targetedBodyPart == null)
        {
            if (throwPopups)
                _popupSystem.PopupEntity(Loc.GetString("does-not-exist-rebell"), target, user, PopupType.MediumCaution);
            return false;
        }

        var totalBleeds =
            _wounds.GetWoundableWoundsWithComp<BleedInflicterComponent>(targetedBodyPart.Value.Id)
                .Select(woundEnt => woundEnt.Comp2)
                .Where(bleeds => bleeds.IsBleeding)
                .Aggregate(FixedPoint2.Zero, (current, bleeds) => current + bleeds.BleedingAmountRaw);

        var stuffToHeal =
            healing.Comp.Damage.DamageDict
                .Where(damage => _wounds.HasDamageOfType(targetedBodyPart.Value.Id, damage.Key))
                .ToDictionary(damage => damage.Key, damage => damage.Value);

        if (totalBleeds < healing.Comp.UnableToHealBleedsThreshold)
        {
            if (totalBleeds > 0 && healing.Comp.BloodlossModifier != 0f
                || healing.Comp.ModifyBloodLevel != 0f
                && TryComp<BloodstreamComponent>(target, out var bloodstream)
                && _solutionContainerSystem
                    .ResolveSolution(target.Owner, bloodstream.BloodSolutionName, ref bloodstream.BloodSolution, out var bloodSolution)
                && bloodSolution.Volume < bloodSolution.MaxVolume
                || stuffToHeal.Count != 0)
                return true;
        }

        if (throwPopups)
        {
            if (totalBleeds > healing.Comp.UnableToHealBleedsThreshold)
            {
                _popupSystem.PopupEntity(
                    Loc.GetString("medical-item-cant-use-rebell", ("target", target)),
                    target,
                    user,
                    PopupType.MediumCaution);
            }
            else
            {
                _popupSystem.PopupEntity(
                    Loc.GetString("medical-item-cant-use", ("item", healing.Owner)),
                    target,
                    user,
                    PopupType.Medium);
            }
        }

        return false;
    }
    // backmen edit end

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

        if (healing.Comp.DamageContainers is not null &&
            target.Comp.DamageContainerID is not null &&
            !healing.Comp.DamageContainers.Contains(target.Comp.DamageContainerID.Value))
        {
            return false;
        }

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
            : healing.Comp.Delay * GetScaledHealingPenalty(healing);

        var doAfterEventArgs =
            new DoAfterArgs(EntityManager, user, delay, new HealingDoAfterEvent(), target, target: target, used: healing)
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
    /// <param name="uid"></param>
    /// <param name="component"></param>
    /// <returns></returns>
    public float GetScaledHealingPenalty(Entity<HealingComponent> healing)
    {
        var output = healing.Comp.Delay;
        if (!TryComp<MobThresholdsComponent>(healing, out var mobThreshold) ||
            !TryComp<DamageableComponent>(healing, out var damageable))
            return output;

        if (!_mobThresholdSystem.TryGetThresholdForState(healing, MobState.Critical, out var amount, mobThreshold))
            return 1;

        var percentDamage = (float)(damageable.TotalDamage / amount);
        if (TryComp<ConsciousnessComponent>(healing, out var consciousness))
        {
            percentDamage = (float)(consciousness.Threshold / (consciousness.Cap - consciousness.Consciousness)); // backmen edit; consciousness
        }

        //basically make it scale from 1 to the multiplier.
        var modifier = percentDamage * (healing.Comp.SelfHealPenaltyMultiplier - 1) + 1;
        return Math.Max(modifier, 1);
    }
}
