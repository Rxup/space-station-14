using System.Linq;
using Content.Shared.Backmen.Surgery.Body.Events;
using Content.Shared.Backmen.Surgery.Consciousness;
using Content.Shared.Backmen.Surgery.Consciousness.Components;
using Content.Shared.Backmen.Surgery.Consciousness.Systems;
using Content.Shared.Backmen.Surgery.Pain.Components;
using Content.Shared.Backmen.Targeting;
using Content.Shared.Body.Events;
using Content.Shared.Body.Systems;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Rejuvenate;
using JetBrains.Annotations;
using Robust.Shared.Random;

namespace Content.Server.Backmen.Surgery.Consciousness.Systems;

public sealed class ServerConsciousnessSystem : ConsciousnessSystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ConsciousnessComponent, ComponentInit>(OnConsciousnessInit);

        SubscribeLocalEvent<ConsciousnessComponent, MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<ConsciousnessComponent, HandleCustomDamage>(OnConsciousnessDamaged);

        // To prevent people immediately falling down as rejuvenated
        SubscribeLocalEvent<ConsciousnessComponent, RejuvenateEvent>(OnRejuvenate, after: [typeof(SharedBodySystem)]);

        SubscribeLocalEvent<ConsciousnessRequiredComponent, BodyPartAddedEvent>(OnBodyPartAdded);
        SubscribeLocalEvent<ConsciousnessRequiredComponent, BodyPartRemovedEvent>(OnBodyPartRemoved);

        SubscribeLocalEvent<ConsciousnessRequiredComponent, OrganAddedToBodyEvent>(OnOrganAdded);
        SubscribeLocalEvent<ConsciousnessRequiredComponent, OrganRemovedFromBodyEvent>(OnOrganRemoved);
    }

    private const string NerveSystemIdentifier = "nerveSystem";

    private void OnConsciousnessDamaged(
        EntityUid uid,
        ConsciousnessComponent component,
        ref HandleCustomDamage args)
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
                            actuallyInducedDamage.DamageDict[damagePair.Key] = 0;
                            continue;
                        }

                        var damage = new DamageSpecifier();
                        damage.DamageDict.Add(damagePair.Key, damagePair.Value);

                        var beforePart = new BeforeDamageChangedEvent(damage, args.Origin, args.CanBeCancelled);
                        RaiseLocalEvent(mostDamaged.Value, ref beforePart);

                        if (beforePart.Cancelled)
                            continue;

                        actuallyInducedDamage.DamageDict[damagePair.Key] =
                            Wound.GetWoundsChanged(mostDamaged.Value, args.Origin, damage, component: mostDamaged.Value).DamageDict[damagePair.Key];
                    }
                    else
                    {
                        var bodyParts = Body.GetBodyChildren(uid).ToList();

                        actuallyInducedDamage.DamageDict[damagePair.Key] = 0;
                        if (bodyParts.Count == 0)
                            continue;

                        var damagePerPart = new DamageSpecifier();
                        damagePerPart.DamageDict.Add(damagePair.Key, damagePair.Value / bodyParts.Count);

                        foreach (var bodyPart in bodyParts)
                        {
                            var beforePart = new BeforeDamageChangedEvent(damagePerPart, args.Origin, args.CanBeCancelled);
                            RaiseLocalEvent(bodyPart.Id, ref beforePart);

                            if (beforePart.Cancelled)
                                continue;

                            actuallyInducedDamage.DamageDict[damagePair.Key] +=
                                Wound.GetWoundsChanged(bodyPart.Id, args.Origin, damagePerPart).DamageDict[damagePair.Key];
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
                var possibleTargets = Body.GetBodyChildrenOfType(uid, partType, symmetry: symmetry).ToList();

                if (possibleTargets.Count == 0)
                    possibleTargets = Body.GetBodyChildren(uid).ToList();

                // No body parts at all?
                if (possibleTargets.Count == 0)
                {
                    // empty
                    actuallyInducedDamage = new DamageSpecifier();
                    break;
                }

                var chosenTarget = Random.PickAndTake(possibleTargets);

                var beforePart = new BeforeDamageChangedEvent(args.Damage, args.Origin, args.CanBeCancelled);
                RaiseLocalEvent(chosenTarget.Id, ref beforePart);

                if (!beforePart.Cancelled)
                    actuallyInducedDamage = Wound.GetWoundsChanged(chosenTarget.Id, args.Origin, args.Damage);
                break;
            }
        }

        args.Damage = actuallyInducedDamage;
        args.Handled = true;
    }

    private void OnMobStateChanged(EntityUid uid, ConsciousnessComponent component, MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead)
            return;

        AddConsciousnessModifier(uid, uid, -component.Cap, "DeathThreshold", ConsciousnessModType.Pain, consciousness: component);
        // To prevent people from suddenly resurrecting while being dead. whoops

        foreach (var multiplier in
                 component.Multipliers.Where(multiplier => multiplier.Value.Type != ConsciousnessModType.Pain))
        {
            RemoveConsciousnessMultiplier(uid, multiplier.Key.Item1, multiplier.Key.Item2, component);
        }

        foreach (var multiplier in
                 component.Modifiers.Where(multiplier => multiplier.Value.Type != ConsciousnessModType.Pain))
        {
            RemoveConsciousnessModifier(uid, multiplier.Key.Item1, multiplier.Key.Item2, component);
        }
    }

    private void OnRejuvenate(EntityUid uid, ConsciousnessComponent component, RejuvenateEvent args)
    {
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
                    .Where(multiplier => multiplier.Value.Type != ConsciousnessModType.Pain)
                    .Select(multiplier => multiplier.Key)
                    .ToArray())
        {
            RemoveConsciousnessMultiplier(uid, key.Item1, key.Item2, component);
        }

        foreach (var key in component.Modifiers
                     .Where(modifier => modifier.Value.Type != ConsciousnessModType.Pain)
                     .Select(modifier => modifier.Key)
                     .ToArray())
        {
            RemoveConsciousnessModifier(uid, key.Item1, key.Item2, component);
        }

        CheckRequiredParts(uid, component);
        ForceConscious(uid, TimeSpan.FromSeconds(1f), component);
    }

    private void OnBodyPartAdded(EntityUid uid, ConsciousnessRequiredComponent component, ref BodyPartAddedEvent args)
    {
        if (args.Part.Comp.Body == null || !ConsciousnessQuery.TryComp(args.Part.Comp.Body, out var consciousness))
            return;

        consciousness.RequiredConsciousnessParts[component.Identifier] = (uid, component.CausesDeath, false);
        CheckRequiredParts(args.Part.Comp.Body.Value, consciousness);
    }

    private void OnBodyPartRemoved(EntityUid uid, ConsciousnessRequiredComponent component, ref BodyPartRemovedEvent args)
    {
        if (args.Part.Comp.Body == null || !ConsciousnessQuery.TryComp(args.Part.Comp.Body.Value, out var consciousness))
            return;

        if (!consciousness.RequiredConsciousnessParts.TryGetValue(component.Identifier, out var value))
        {
            Log.Warning($"ConsciousnessRequirementPart with identifier {component.Identifier}:{uid} not found on body:{args.Part.Comp.Body}");
            return;
        }

        consciousness.RequiredConsciousnessParts[component.Identifier] = (uid, value.Item2, true);
        CheckRequiredParts(args.Part.Comp.Body.Value, consciousness);
    }

    private void OnOrganAdded(EntityUid uid, ConsciousnessRequiredComponent component, ref OrganAddedToBodyEvent args)
    {
        if (!ConsciousnessQuery.TryComp(args.Body, out var consciousness))
            return;

        consciousness.RequiredConsciousnessParts[component.Identifier] = (uid, component.CausesDeath, false);

        if (component.Identifier == NerveSystemIdentifier)
        {
            var nerveSys = Comp<NerveSystemComponent>(uid);
            nerveSys.RootNerve = args.Part;
            consciousness.NerveSystem = (uid, nerveSys);
        }

        CheckRequiredParts(args.Body, consciousness);
    }

    private void OnOrganRemoved(EntityUid uid, ConsciousnessRequiredComponent component, ref OrganRemovedFromBodyEvent args)
    {
        if (TerminatingOrDeleted(args.OldBody) || !ConsciousnessQuery.TryComp(args.OldBody, out var consciousness))
            return;

        if (!consciousness.RequiredConsciousnessParts.TryGetValue(component.Identifier, out var value))
        {
            Log.Warning($"ConsciousnessRequirementPart with identifier {component.Identifier}:{uid} not found on body:{args.OldBody}");
            return;
        }

        consciousness.RequiredConsciousnessParts[component.Identifier] = (uid, value.Item2, true);
        CheckRequiredParts(args.OldBody, consciousness);
    }

    private void OnConsciousnessInit(EntityUid uid, ConsciousnessComponent consciousness, ComponentInit args)
    {
        if (consciousness.RawConsciousness <= 0)
        {
            consciousness.RawConsciousness = consciousness.Cap;
            Dirty(uid, consciousness);
        }

        CheckConscious(uid, consciousness);
    }

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
                RemoveConsciousnessModifier(ent, modifier.Item1, modifier.Item2, consciousness);
            }

            foreach (var multiplier in
                     consciousness.Multipliers
                         .Where(m => m.Value.Time < Timing.CurTime)
                         .Select(m => m.Key)
                         .ToArray())
            {
                RemoveConsciousnessMultiplier(ent, multiplier.Item1, multiplier.Item2, consciousness);
            }

            if (consciousness.PassedOutTime < Timing.CurTime && consciousness.PassedOut)
            {
                consciousness.PassedOut = false;
                CheckConscious(ent, consciousness);
            }

            if (consciousness.ForceConsciousnessTime < Timing.CurTime && consciousness.ForceConscious)
            {
                consciousness.ForceConscious = false;
                CheckConscious(ent, consciousness);
            }
        }
    }

    #region Helpers

    [PublicAPI]
    public override bool CheckConscious(
        EntityUid target,
        ConsciousnessComponent? consciousness = null,
        MobStateComponent? mobState = null)
    {
        if (TerminatingOrDeleted(target) ||
            !ConsciousnessQuery.Resolve(target, ref consciousness, false)
            || !MobStateQuery.Resolve(target, ref mobState, false))
            return false;

        var shouldBeConscious =
            consciousness.Consciousness > consciousness.Threshold || consciousness is { ForceUnconscious: false, ForceConscious: true };

        var ev = new ConsciousnessUpdatedEvent(shouldBeConscious);
        RaiseLocalEvent(target, ref ev);

        SetConscious(target, shouldBeConscious, consciousness);
        UpdateMobState(target, consciousness, mobState);

        return shouldBeConscious;
    }

    [PublicAPI]
    public override void ForcePassOut(
        EntityUid target,
        TimeSpan time,
        ConsciousnessComponent? consciousness = null)
    {
        if (!ConsciousnessQuery.Resolve(target, ref consciousness, false))
            return;

        consciousness.PassedOut = true;
        consciousness.PassedOutTime = Timing.CurTime + time;

        CheckConscious(target, consciousness);
    }

    [PublicAPI]
    public override void ForceConscious(
        EntityUid target,
        TimeSpan time,
        ConsciousnessComponent? consciousness = null)
    {
        if (!ConsciousnessQuery.Resolve(target, ref consciousness, false))
            return;

        consciousness.ForceConscious = true;
        consciousness.ForceConsciousnessTime = Timing.CurTime + time;

        CheckConscious(target, consciousness);
    }

    [PublicAPI]
    public override void ClearForceEffects(
        EntityUid target,
        ConsciousnessComponent? consciousness = null)
    {
        if (!ConsciousnessQuery.Resolve(target, ref consciousness, false))
            return;

        consciousness.ForceConscious = false;
        consciousness.PassedOut = false;

        CheckConscious(target, consciousness);
    }

    #endregion

    #region Modifiers and Multipliers

    [PublicAPI]
    public override bool AddConsciousnessModifier(EntityUid target,
        EntityUid modifierOwner,
        FixedPoint2 modifier,
        string identifier = "Unspecified",
        ConsciousnessModType type = ConsciousnessModType.Generic,
        TimeSpan? time = null,
        ConsciousnessComponent? consciousness = null)
    {
        if (!ConsciousnessQuery.Resolve(target, ref consciousness, false))
            return false;

        if (!consciousness.Modifiers.TryAdd((modifierOwner, identifier),
                new ConsciousnessModifier(modifier, time.HasValue ? Timing.CurTime + time :  time, type)))
            return false;

        UpdateConsciousnessModifiers(target, consciousness);
        Dirty(target, consciousness);

        return true;
    }

    [PublicAPI]
    public override bool RemoveConsciousnessModifier(EntityUid target,
        EntityUid modifierOwner,
        string identifier,
        ConsciousnessComponent? consciousness = null)
    {
        if (!ConsciousnessQuery.Resolve(target, ref consciousness, false))
            return false;

        if (!consciousness.Modifiers.Remove((modifierOwner, identifier)))
            return false;

        UpdateConsciousnessModifiers(target, consciousness);
        Dirty(target, consciousness);

        return true;
    }

    [PublicAPI]
    public override bool SetConsciousnessModifier(EntityUid target,
        EntityUid modifierOwner,
        FixedPoint2 modifierChange,
        string identifier = "Unspecified",
        ConsciousnessModType type = ConsciousnessModType.Generic,
        TimeSpan? time = null,
        ConsciousnessComponent? consciousness = null)
    {
        if (!ConsciousnessQuery.Resolve(target, ref consciousness, false))
            return false;

        var newModifier = new ConsciousnessModifier(Change: modifierChange, Time: time.HasValue ? Timing.CurTime + time : time, Type: type);
        consciousness.Modifiers[(modifierOwner, identifier)] = newModifier;

        UpdateConsciousnessModifiers(target, consciousness);
        Dirty(target, consciousness);

        return true;
    }

    [PublicAPI]
    public override bool ChangeConsciousnessModifier(EntityUid target,
        EntityUid modifierOwner,
        FixedPoint2 modifierChange,
        string identifier,
        TimeSpan? time = null,
        ConsciousnessComponent? consciousness = null)
    {
        if (!ConsciousnessQuery.Resolve(target, ref consciousness, false) ||
            !consciousness.Modifiers.TryGetValue((modifierOwner, identifier), out var oldModifier))
            return false;

        var newModifier =
            oldModifier with {Change = oldModifier.Change + modifierChange, Time = time.HasValue ? Timing.CurTime + time :  time};

        consciousness.Modifiers[(modifierOwner, identifier)] = newModifier;

        UpdateConsciousnessModifiers(target, consciousness);
        Dirty(target, consciousness);

        return true;
    }

    [PublicAPI]
    public override bool AddConsciousnessMultiplier(EntityUid target,
        EntityUid multiplierOwner,
        FixedPoint2 multiplier,
        string identifier = "Unspecified",
        ConsciousnessModType type = ConsciousnessModType.Generic,
        TimeSpan? time = null,
        ConsciousnessComponent? consciousness = null)
    {
        if (!ConsciousnessQuery.Resolve(target, ref consciousness, false))
            return false;

        if (!consciousness.Multipliers.TryAdd((multiplierOwner, identifier),
                new ConsciousnessMultiplier(multiplier, time.HasValue ? Timing.CurTime + time :  time, type)))
            return false;

        UpdateConsciousnessMultipliers(target, consciousness);
        UpdateConsciousnessModifiers(target, consciousness);

        Dirty(target, consciousness);

        return true;
    }

    [PublicAPI]
    public override bool RemoveConsciousnessMultiplier(EntityUid target,
        EntityUid multiplierOwner,
        string identifier,
        ConsciousnessComponent? consciousness = null)
    {
        if (!ConsciousnessQuery.Resolve(target, ref consciousness, false))
            return false;

        if (!consciousness.Multipliers.Remove((multiplierOwner, identifier)))
            return false;

        UpdateConsciousnessMultipliers(target, consciousness);
        UpdateConsciousnessModifiers(target, consciousness);

        Dirty(target, consciousness);

        return true;
    }

    #endregion
}
