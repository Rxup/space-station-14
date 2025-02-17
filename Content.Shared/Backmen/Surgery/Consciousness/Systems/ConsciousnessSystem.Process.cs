using System.Linq;
using Content.Shared.Backmen.Surgery.Body.Events;
using Content.Shared.Backmen.Surgery.Consciousness.Components;
using Content.Shared.Backmen.Surgery.Pain;
using Content.Shared.Backmen.Surgery.Pain.Components;
using Content.Shared.Body.Events;
using Content.Shared.Body.Organ;
using Content.Shared.Mobs;
using Content.Shared.Rejuvenate;

namespace Content.Shared.Backmen.Surgery.Consciousness.Systems;

public partial class ConsciousnessSystem
{
    private void InitProcess()
    {
        SubscribeLocalEvent<NerveSystemComponent, PainModifierChangedEvent>(OnPainChanged);
        SubscribeLocalEvent<NerveSystemComponent, PainModifierAddedEvent>(OnPainAdded);
        SubscribeLocalEvent<NerveSystemComponent, PainModifierRemovedEvent>(OnPainRemoved);

        SubscribeLocalEvent<ConsciousnessComponent, MobStateChangedEvent>(OnMobStateChanged);

        SubscribeLocalEvent<ConsciousnessComponent, RejuvenateEvent>(OnRejuvenate);

        SubscribeLocalEvent<ConsciousnessRequiredComponent, BodyPartAddedEvent>(OnBodyPartAdded);
        SubscribeLocalEvent<ConsciousnessRequiredComponent, BodyPartRemovedEvent>(OnBodyPartRemoved);

        SubscribeLocalEvent<ConsciousnessRequiredComponent, OrganAddedToBodyEvent>(OnOrganAdded);
        SubscribeLocalEvent<ConsciousnessRequiredComponent, OrganRemovedFromBodyEvent>(OnOrganRemoved);

        SubscribeLocalEvent<ConsciousnessComponent, MapInitEvent>(OnConsciousnessMapInit);
    }

    private const string NerveSystemIdentifier = "nerveSys";

    private void UpdatePassedOut(float frameTime)
    {
        var query = EntityQueryEnumerator<ConsciousnessComponent>();
        while (query.MoveNext(out var ent, out var consciousness))
        {
            if (consciousness.ForceDead)
                continue;

            if (consciousness.PassedOutTime < _timing.CurTime)
                consciousness.PassedOut = false;

            if (consciousness.ForceConsciousnessTime < _timing.CurTime)
                consciousness.ForceConscious = false;

            if (!consciousness.PassedOut || consciousness.ForceConscious)
                CheckConscious(ent, consciousness);
        }
    }

    private void OnPainChanged(EntityUid uid, NerveSystemComponent component, PainModifierChangedEvent args)
    {
        if (!TryComp<OrganComponent>(args.NerveSystem, out var nerveSysOrgan) || !nerveSysOrgan.Body.HasValue)
            return;

        if (!SetConsciousnessModifier(nerveSysOrgan.Body.Value,
                args.NerveSystem,
                -component.Pain,
                null,
                "Pain",
                ConsciousnessModType.Pain))
        {
            AddConsciousnessModifier(nerveSysOrgan.Body.Value,
                args.NerveSystem,
                -component.Pain,
                null,
                "Pain",
                ConsciousnessModType.Pain);
        }
    }

    private void OnPainAdded(EntityUid uid, NerveSystemComponent component, PainModifierAddedEvent args)
    {
        if (!TryComp<OrganComponent>(args.NerveSystem, out var nerveSysOrgan) || !nerveSysOrgan.Body.HasValue)
            return;

        if (!SetConsciousnessModifier(nerveSysOrgan.Body.Value,
                args.NerveSystem,
                -component.Pain,
                null,
                "Pain",
                ConsciousnessModType.Pain))
        {
            AddConsciousnessModifier(nerveSysOrgan.Body.Value,
                args.NerveSystem,
                -component.Pain,
                null,
                "Pain",
                ConsciousnessModType.Pain);
        }
    }

    private void OnPainRemoved(EntityUid uid, NerveSystemComponent component, PainModifierRemovedEvent args)
    {
        if (!TryComp<OrganComponent>(args.NerveSystem, out var nerveSysOrgan) || !nerveSysOrgan.Body.HasValue)
            return;

        if (args.CurrentPain <= 0)
        {
            RemoveConsciousnessModifer(nerveSysOrgan.Body.Value, args.NerveSystem, "Pain");
        }
        else
        {
            SetConsciousnessModifier(nerveSysOrgan.Body.Value,
                args.NerveSystem,
                -component.Pain,
                null,
                "Pain",
                type: ConsciousnessModType.Pain);
        }
    }

    private void OnMobStateChanged(EntityUid uid, ConsciousnessComponent component, MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead)
            return;

        AddConsciousnessModifier(uid, uid, -component.Cap, component, "DeathThreshold", ConsciousnessModType.Pain);
        // To prevent people from suddenly resurrecting while being dead. whoops

        foreach (var multiplier in
                 component.Multipliers.Where(multiplier => multiplier.Value.Type != ConsciousnessModType.Pain))
        {
            RemoveConsciousnessMultiplier(uid, multiplier.Key.Item1, multiplier.Key.Item2, component);
        }

        foreach (var multiplier in
                 component.Modifiers.Where(multiplier => multiplier.Value.Type != ConsciousnessModType.Pain))
        {
            RemoveConsciousnessModifer(uid, multiplier.Key.Item1, multiplier.Key.Item2, component);
        }
    }

    private void OnRejuvenate(EntityUid uid, ConsciousnessComponent component, ref RejuvenateEvent args)
    {
        foreach (var multiplier in
                 component.Multipliers.Where(multiplier => multiplier.Value.Type == ConsciousnessModType.Pain))
        {
            RemoveConsciousnessMultiplier(uid, multiplier.Key.Item1, multiplier.Key.Item2, component);
        }

        foreach (var modifier in
                 component.Modifiers.Where(modifier => modifier.Value.Type == ConsciousnessModType.Pain))
        {
            RemoveConsciousnessModifer(uid, modifier.Key.Item1, modifier.Key.Item2, component);
        }

        foreach (var painModifier in component.NerveSystem.Comp.Modifiers)
        {
            _pain.TryRemovePainModifier(component.NerveSystem.Owner, painModifier.Key.Item1, painModifier.Key.Item2, component.NerveSystem.Comp);
        }

        foreach (var painMultiplier in component.NerveSystem.Comp.Multipliers)
        {
            _pain.TryRemovePainMultiplier(component.NerveSystem.Owner, painMultiplier.Key, component.NerveSystem.Comp);
        }

        foreach (var nerve in component.NerveSystem.Comp.Nerves)
        {
            foreach (var painFeelsModifier in nerve.Value.PainFeelingModifiers)
            {
                _pain.TryRemovePainFeelsModifier(painFeelsModifier.Key.Item1, painFeelsModifier.Key.Item2, nerve.Key, nerve.Value);
            }
        }
    }

    private void OnConsciousnessMapInit(EntityUid uid, ConsciousnessComponent consciousness, MapInitEvent args)
    {
        if (consciousness.RawConsciousness < 0)
        {
            consciousness.RawConsciousness = consciousness.Cap;
            Dirty(uid, consciousness);
        }

        CheckConscious(uid, consciousness);
    }

    private void OnBodyPartAdded(EntityUid uid, ConsciousnessRequiredComponent component, ref BodyPartAddedEvent args)
    {
        if (args.Part.Comp.Body == null ||
            !TryComp<ConsciousnessComponent>(args.Part.Comp.Body, out var consciousness))
            return;

        if (consciousness.RequiredConsciousnessParts.TryGetValue(component.Identifier, out var value) && value.Item1 != null && value.Item1 != uid)
        {
            _sawmill.Warning($"ConsciousnessRequirementPart with duplicate Identifier {component.Identifier}:{uid} added to a body:" +
                        $" {args.Part.Comp.Body} this will result in unexpected behaviour!");
        }

        consciousness.RequiredConsciousnessParts[component.Identifier] = (uid, component.CausesDeath, false);

        CheckRequiredParts(args.Part.Comp.Body.Value, consciousness);
    }

    private void OnBodyPartRemoved(EntityUid uid, ConsciousnessRequiredComponent component, ref BodyPartRemovedEvent args)
    {
        if (args.Part.Comp.Body == null || !TryComp<ConsciousnessComponent>(args.Part.Comp.Body.Value, out var consciousness))
            return;

        if (!consciousness.RequiredConsciousnessParts.TryGetValue(component.Identifier, out var value))
        {
            _sawmill.Warning($"ConsciousnessRequirementPart with identifier {component.Identifier}:{uid} not found on body:{args.Part.Comp.Body}");
            return;
        }

        consciousness.RequiredConsciousnessParts[component.Identifier] = (uid, value.Item2, true);

        CheckRequiredParts(args.Part.Comp.Body.Value, consciousness);
    }

    private void OnOrganAdded(EntityUid uid, ConsciousnessRequiredComponent component, ref OrganAddedToBodyEvent args)
    {
        if (!TryComp<ConsciousnessComponent>(args.Body, out var consciousness))
            return;

        if (consciousness.RequiredConsciousnessParts.TryGetValue(component.Identifier, out var value) && value.Item1 != null && value.Item1 != uid)
        {
            _sawmill.Warning($"ConsciousnessRequirementPart with duplicate Identifier {component.Identifier}:{uid} added to a body:" +
                             $" {args.Body} this will result in unexpected behaviour! Old {component.Identifier} wielder: {value.Item1}");
        }

        consciousness.RequiredConsciousnessParts[component.Identifier] = (uid, component.CausesDeath, false);

        if (component.Identifier == NerveSystemIdentifier)
            consciousness.NerveSystem = (uid, Comp<NerveSystemComponent>(uid));

        CheckRequiredParts(args.Body, consciousness);
    }

    private void OnOrganRemoved(EntityUid uid, ConsciousnessRequiredComponent component, ref OrganRemovedFromBodyEvent args)
    {
        if (!TryComp<ConsciousnessComponent>(args.OldBody, out var consciousness))
            return;

        if (!consciousness.RequiredConsciousnessParts.TryGetValue(component.Identifier, out var value))
        {
            _sawmill.Warning($"ConsciousnessRequirementPart with identifier {component.Identifier}:{uid} not found on body:{args.OldBody}");
            return;
        }

        consciousness.RequiredConsciousnessParts[component.Identifier] = (uid, value.Item2, true);

        CheckRequiredParts(args.OldBody, consciousness);
    }
}
