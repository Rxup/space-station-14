using Content.Shared.Backmen.Surgery.Body.Events;
using Content.Shared.Backmen.Surgery.Consciousness.Components;
using Content.Shared.Backmen.Surgery.Pain;
using Content.Shared.Backmen.Surgery.Pain.Components;
using Content.Shared.Body.Events;
using Content.Shared.Body.Organ;
using Content.Shared.Body.Part;

namespace Content.Shared.Backmen.Surgery.Consciousness.Systems;

public partial class ConsciousnessSystem
{
    private void InitProcess()
    {
        SubscribeLocalEvent<BodyPartComponent, PainModifierChangedEvent>(OnPainChanged);
        SubscribeLocalEvent<BodyPartComponent, PainModifierAddedEvent>(OnPainAdded);
        SubscribeLocalEvent<BodyPartComponent, PainModifierRemovedEvent>(OnPainRemoved);

        SubscribeLocalEvent<ConsciousnessRequiredComponent, ComponentInit>(OnConsciousnessPartInit);

        SubscribeLocalEvent<ConsciousnessRequiredComponent, BodyPartAddedEvent>(OnBodyPartAdded);
        SubscribeLocalEvent<ConsciousnessRequiredComponent, BodyPartRemovedEvent>(OnBodyPartRemoved);

        SubscribeLocalEvent<ConsciousnessRequiredComponent, OrganAddedToBodyEvent>(OnOrganAdded);
        SubscribeLocalEvent<ConsciousnessRequiredComponent, OrganRemovedFromBodyEvent>(OnOrganRemoved);

        SubscribeLocalEvent<ConsciousnessComponent, MapInitEvent>(OnConsciousnessMapInit);
    }

    private void OnPainChanged(EntityUid uid, BodyPartComponent component, PainModifierChangedEvent args)
    {
        if (!TryComp<OrganComponent>(args.NerveSystem, out var nerveSysOrgan) ||
            !TryComp<NerveSystemComponent>(args.NerveSystem, out var nerveSys))
            return;

        if (!SetConsciousnessModifier(nerveSysOrgan.Body!.Value,
                args.NerveSystem,
                -nerveSys.Pain,
                null,
                ConsciousnessModType.Pain))
        {
            AddConsciousnessModifier(nerveSysOrgan.Body!.Value,
                args.NerveSystem,
                -nerveSys.Pain,
                null,
                "Pain",
                ConsciousnessModType.Pain);
        }
    }

    private void OnPainAdded(EntityUid uid, BodyPartComponent component, PainModifierAddedEvent args)
    {
        if (!TryComp<OrganComponent>(args.NerveSystem, out var nerveSysOrgan) ||
            !TryComp<NerveSystemComponent>(args.NerveSystem, out var nerveSys))
            return;

        if (!SetConsciousnessModifier(nerveSysOrgan.Body!.Value,
                args.NerveSystem,
                -nerveSys.Pain,
                null,
                ConsciousnessModType.Pain))
        {
            AddConsciousnessModifier(nerveSysOrgan.Body!.Value,
                args.NerveSystem,
                -nerveSys.Pain,
                null,
                "Pain",
                ConsciousnessModType.Pain);
        }
    }

    private void OnPainRemoved(EntityUid uid, BodyPartComponent component, PainModifierRemovedEvent args)
    {
        if (!TryComp<OrganComponent>(args.NerveSystem, out var nerveSysOrgan) ||
            !TryComp<NerveSystemComponent>(args.NerveSystem, out var nerveSys))
            return;

        if (args.CurrentPain <= 0)
        {
            RemoveConsciousnessModifer(nerveSysOrgan.Body!.Value,
                args.NerveSystem,
                type: ConsciousnessModType.Pain);
        }
        else
        {
            SetConsciousnessModifier(nerveSysOrgan.Body!.Value,
                args.NerveSystem,
                -nerveSys.Pain,
                type: ConsciousnessModType.Pain);
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

    private void OnConsciousnessPartInit(EntityUid uid, ConsciousnessRequiredComponent component, ComponentInit args)
    {
        if (_net.IsClient)
            return;

        EntityUid? body = null;
        if (TryComp<BodyPartComponent>(uid, out var bodyPart))
        {
            body = bodyPart.Body;
        }
        else if (TryComp<OrganComponent>(uid, out var organ))
        {
            body = organ.Body;
        }

        if (!TryComp<ConsciousnessComponent>(body, out var consciousness))
            return;

        if (!consciousness.RequiredConsciousnessParts.TryAdd(component.Identifier, (uid, component.CausesDeath, false)))
        {
            _sawmill.Warning($"ConsciousnessRequirementPart with duplicate Identifier {component.Identifier}:{uid} added to a body:" +
                             $" {uid} this will result in unexpected behaviour!");
        }
    }

    private void OnBodyPartAdded(EntityUid uid, ConsciousnessRequiredComponent component, ref BodyPartAddedEvent args)
    {
        if (_net.IsClient)
            return;

        if (args.Part.Comp.Body == null ||
            !TryComp<ConsciousnessComponent>(args.Part.Comp.Body, out var consciousness))
            return;

        if (!consciousness.RequiredConsciousnessParts.ContainsKey(component.Identifier)
            && consciousness.RequiredConsciousnessParts[component.Identifier].Item1 != null)
        {
            _sawmill.Warning($"ConsciousnessRequirementPart with duplicate Identifier {component.Identifier}:{uid} added to a body:" +
                        $" {args.Part.Comp.Body} this will result in unexpected behaviour!");
        }

        consciousness.RequiredConsciousnessParts[component.Identifier] = (uid, component.CausesDeath, false);

        CheckRequiredParts(args.Part.Comp.Body.Value, consciousness);
    }

    private void OnBodyPartRemoved(EntityUid uid, ConsciousnessRequiredComponent component, ref BodyPartRemovedEvent args)
    {
        if (_net.IsClient)
            return;

        if (args.Part.Comp.Body == null || !TryComp<ConsciousnessComponent>(args.Part.Comp.Body.Value, out var consciousness))
            return;

        if (!consciousness.RequiredConsciousnessParts.TryGetValue(component.Identifier, out var value))
        {
            _sawmill.Warning($"ConsciousnessRequirementPart with identifier {component.Identifier} not found on body:{uid}");
            return;
        }

        consciousness.RequiredConsciousnessParts[component.Identifier] =
            (uid, value.Item2, true);

        CheckRequiredParts(args.Part.Comp.Body.Value, consciousness);
    }

    private void OnOrganAdded(EntityUid uid, ConsciousnessRequiredComponent component, ref OrganAddedToBodyEvent args)
    {
        if (_net.IsClient)
            return;

        if (!TryComp<ConsciousnessComponent>(args.Body, out var consciousness))
            return;

        if (!consciousness.RequiredConsciousnessParts.TryGetValue(component.Identifier, out var value) && value.Item1 != null)
        {
            _sawmill.Warning($"ConsciousnessRequirementPart with duplicate Identifier {component.Identifier}:{uid} added to a body:" +
                        $" {args.Body} this will result in unexpected behaviour!");
        }

        consciousness.RequiredConsciousnessParts[component.Identifier] = (uid, component.CausesDeath, false);

        CheckRequiredParts(args.Body, consciousness);
    }

    private void OnOrganRemoved(EntityUid uid, ConsciousnessRequiredComponent component, ref OrganRemovedFromBodyEvent args)
    {
        if (!TryComp<ConsciousnessComponent>(args.OldBody, out var consciousness))
            return;

        if (!consciousness.RequiredConsciousnessParts.TryGetValue(component.Identifier, out var value))
        {
            _sawmill.Warning($"ConsciousnessRequirementPart with identifier {component.Identifier} not found on body:{uid}");
            return;
        }

        consciousness.RequiredConsciousnessParts[component.Identifier] =
            (uid, value.Item2, true);

        CheckRequiredParts(args.OldBody, consciousness);
    }
}
