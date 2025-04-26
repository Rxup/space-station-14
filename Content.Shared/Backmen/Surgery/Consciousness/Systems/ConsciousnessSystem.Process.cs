using System.Linq;
using Content.Shared.Backmen.Surgery.Body.Events;
using Content.Shared.Backmen.Surgery.Consciousness.Components;
using Content.Shared.Backmen.Surgery.Pain.Components;
using Content.Shared.Backmen.Targeting;
using Content.Shared.Body.Events;
using Content.Shared.Body.Systems;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Mobs;
using Content.Shared.Rejuvenate;
using Robust.Shared.Random;

namespace Content.Shared.Backmen.Surgery.Consciousness.Systems;

public partial class ConsciousnessSystem
{
    private void InitProcess()
    {
        SubscribeLocalEvent<ConsciousnessComponent, MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<ConsciousnessComponent, CheckForCustomHandlerEvent>(OnConsciousnessDamaged);

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
        ref CheckForCustomHandlerEvent args)
    {
        var actuallyInducedDamage = new DamageSpecifier(args.Damage);
        switch (args.TargetPart)
        {
            case TargetBodyPart.All:
            {
                foreach (var damagePair in args.Damage.DamageDict)
                {
                    if (damagePair.Value == 0)
                        continue;

                    var damageGroup = (from @group in _proto.EnumeratePrototypes<DamageGroupPrototype>()
                        where @group.DamageTypes.Contains(damagePair.Key)
                        select @group).FirstOrDefault();

                    if (damagePair.Value < 0)
                    {
                        if (!_wound.TryGetWoundableWithMostDamage(
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
                            _wound.GetWoundsChanged(mostDamaged.Value, args.Origin, damage, mostDamaged.Value).DamageDict[damagePair.Key];
                    }
                    else
                    {
                        var bodyParts = Body.GetBodyChildren(uid).ToList();

                        var damagePerPart = new DamageSpecifier();
                        damagePerPart.DamageDict.Add(damagePair.Key, damagePair.Value / bodyParts.Count);

                        actuallyInducedDamage.DamageDict[damagePair.Key] = 0;
                        foreach (var bodyPart in bodyParts)
                        {
                            var beforePart = new BeforeDamageChangedEvent(damagePerPart, args.Origin, args.CanBeCancelled);
                            RaiseLocalEvent(bodyPart.Id, ref beforePart);

                            if (beforePart.Cancelled)
                                continue;

                            actuallyInducedDamage.DamageDict[damagePair.Key] +=
                                _wound.GetWoundsChanged(bodyPart.Id, args.Origin, damagePerPart).DamageDict[damagePair.Key];
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
                    return;

                var chosenTarget = _random.PickAndTake(possibleTargets);

                var beforePart = new BeforeDamageChangedEvent(args.Damage, args.Origin, args.CanBeCancelled);
                RaiseLocalEvent(chosenTarget.Id, ref beforePart);

                if (!beforePart.Cancelled)
                    actuallyInducedDamage = _wound.GetWoundsChanged(chosenTarget.Id, args.Origin, args.Damage);
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
        foreach (var painModifier in component.NerveSystem.Comp.Modifiers)
        {
            _pain.TryRemovePainModifier(component.NerveSystem.Owner, painModifier.Key.Item1, painModifier.Key.Item2, component.NerveSystem.Comp);
        }

        foreach (var painMultiplier in component.NerveSystem.Comp.Multipliers)
        {
            _pain.TryRemovePainMultiplier(component.NerveSystem.Owner, painMultiplier.Key, component.NerveSystem.Comp);
        }

        foreach (var multiplier in
                 component.Multipliers.Where(multiplier => multiplier.Value.Type == ConsciousnessModType.Pain))
        {
            RemoveConsciousnessMultiplier(uid, multiplier.Key.Item1, multiplier.Key.Item2, component);
        }

        foreach (var modifier in
                 component.Modifiers.Where(modifier => modifier.Value.Type == ConsciousnessModType.Pain))
        {
            RemoveConsciousnessModifier(uid, modifier.Key.Item1, modifier.Key.Item2, component);
        }

        foreach (var nerve in component.NerveSystem.Comp.Nerves)
        {
            foreach (var painFeelsModifier in nerve.Value.PainFeelingModifiers)
            {
                _pain.TryRemovePainFeelsModifier(painFeelsModifier.Key.Item1, painFeelsModifier.Key.Item2, nerve.Key, nerve.Value);
            }
        }

        CheckRequiredParts(uid, component);
        ForceConscious(uid, TimeSpan.FromSeconds(1f), component);
    }

    private void OnBodyPartAdded(EntityUid uid, ConsciousnessRequiredComponent component, ref BodyPartAddedEvent args)
    {
        if (!Timing.IsFirstTimePredicted)
            return;

        if (args.Part.Comp.Body == null ||
            !TryComp<ConsciousnessComponent>(args.Part.Comp.Body, out var consciousness))
            return;

        consciousness.RequiredConsciousnessParts[component.Identifier] = (uid, component.CausesDeath, false);
        CheckRequiredParts(args.Part.Comp.Body.Value, consciousness);
    }

    private void OnBodyPartRemoved(EntityUid uid, ConsciousnessRequiredComponent component, ref BodyPartRemovedEvent args)
    {
        if (!Timing.IsFirstTimePredicted)
            return;

        if (args.Part.Comp.Body == null || !TryComp<ConsciousnessComponent>(args.Part.Comp.Body.Value, out var consciousness))
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
        if (!Timing.IsFirstTimePredicted)
            return;

        if (!TryComp<ConsciousnessComponent>(args.Body, out var consciousness))
            return;

        consciousness.RequiredConsciousnessParts[component.Identifier] = (uid, component.CausesDeath, false);

        if (component.Identifier == NerveSystemIdentifier)
            consciousness.NerveSystem = (uid, Comp<NerveSystemComponent>(uid));

        CheckRequiredParts(args.Body, consciousness);
    }

    private void OnOrganRemoved(EntityUid uid, ConsciousnessRequiredComponent component, ref OrganRemovedFromBodyEvent args)
    {
        if (!Timing.IsFirstTimePredicted)
            return;

        if (!TryComp<ConsciousnessComponent>(args.OldBody, out var consciousness))
            return;

        if (!consciousness.RequiredConsciousnessParts.TryGetValue(component.Identifier, out var value))
        {
            Log.Warning($"ConsciousnessRequirementPart with identifier {component.Identifier}:{uid} not found on body:{args.OldBody}");
            return;
        }

        consciousness.RequiredConsciousnessParts[component.Identifier] = (uid, value.Item2, true);
        CheckRequiredParts(args.OldBody, consciousness);
    }
}
