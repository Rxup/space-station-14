using System.Linq;
using Content.Shared.Backmen.Surgery.Conditions;
using Content.Shared.Backmen.Surgery.Consciousness.Systems;
using Content.Shared.Backmen.Surgery.Pain;
using Content.Shared.Backmen.Surgery.Pain.Systems;
using Content.Shared.Backmen.Surgery.Steps.Parts;
using Content.Shared.Backmen.Surgery.Traumas.Components;
using Content.Shared.Backmen.Surgery.Traumas.Systems;
using Content.Shared.Backmen.Surgery.Wounds.Components;
using Content.Shared.Backmen.Surgery.Wounds.Systems;
using Content.Shared.Medical.Surgery.Conditions;
using Content.Shared.Body;
using Content.Shared.Body.Part;
using Content.Shared.Backmen.Body.OrganRelations;
using Content.Shared.Backmen.Body.Systems;
using Content.Shared.Backmen.Targeting;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Medical.Surgery.Steps;
using Content.Shared.Buckle.Components;
using Content.Shared.DoAfter;
using Content.Shared.Mobs.Systems;
using Content.Shared.GameTicking;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Inventory;
using Content.Shared.Popups;
using Content.Shared.Standing;
using Content.Shared.StatusEffectNew;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared.Backmen.Surgery;

public abstract partial class SharedSurgerySystem : EntitySystem
{
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private IComponentFactory _compFactory = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private BkmBodySharedSystem _body = default!;
    [Dependency] private BodySystem _organBody = default!;
    [Dependency] private SharedTargetingSystem _targeting = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private ItemSlotsSystem _itemSlotsSystem = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private RotateToFaceSystem _rotateToFace = default!;
    [Dependency] private StandingStateSystem _standing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private WoundSystem _wounds = default!;
    [Dependency] private TraumaSystem _trauma = default!;
    [Dependency] private ConsciousnessSystem _consciousness = default!;
    [Dependency] private OrganRelationInitializerSystem _organRelations = default!;
    [Dependency] private OrganRelationSystem _organRelation = default!;
    [Dependency] private BkmDetachedBodySystem _detachedBodies = default!;
    [Dependency] private SharedVisualBodySystem _visualBody = default!;
    [Dependency] private PainSystem _pain = default!;
    [Dependency] private StatusEffectsSystem _statusEffects = default!;

    private readonly Dictionary<EntProtoId, EntityUid> _surgeries = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestartCleanup);

        SubscribeLocalEvent<SurgeryTargetComponent, SurgeryDoAfterEvent>(OnTargetDoAfter);
        SubscribeLocalEvent<SurgeryCloseIncisionConditionComponent, SurgeryValidEvent>(OnCloseIncisionValid);
        //SubscribeLocalEvent<SurgeryLarvaConditionComponent, SurgeryValidEvent>(OnLarvaValid);
        SubscribeLocalEvent<SurgeryComponentConditionComponent, SurgeryValidEvent>(OnComponentConditionValid);
        SubscribeLocalEvent<SurgeryBodyStatusEffectConditionComponent, SurgeryValidEvent>(OnStatusEffectConditionValid);
        SubscribeLocalEvent<SurgeryPartConditionComponent, SurgeryValidEvent>(OnPartConditionValid);
        SubscribeLocalEvent<SurgeryOrganConditionComponent, SurgeryValidEvent>(OnOrganConditionValid);
        SubscribeLocalEvent<SurgeryWoundedConditionComponent, SurgeryValidEvent>(OnWoundedValid);
        SubscribeLocalEvent<SurgeryPartRemovedConditionComponent, SurgeryValidEvent>(OnPartRemovedConditionValid);
        SubscribeLocalEvent<SurgeryOrganCategoryMissingConditionComponent, SurgeryValidEvent>(OnOrganCategoryMissingConditionValid);
        SubscribeLocalEvent<SurgeryBothHumanLegsMissingConditionComponent, SurgeryValidEvent>(OnBothHumanLegsMissingValid);
        SubscribeLocalEvent<SurgeryOrganGraftAttachComponent, SurgeryValidEvent>(OnGraftAttachValid);
        SubscribeLocalEvent<SurgeryOrganGraftDetachComponent, SurgeryValidEvent>(OnGraftDetachValid);
        SubscribeLocalEvent<SurgeryArachneGraftOrganConditionComponent, SurgeryValidEvent>(OnArachneGraftOrganValid);
        SubscribeLocalEvent<SurgeryPartPresentConditionComponent, SurgeryValidEvent>(OnPartPresentConditionValid);
        SubscribeLocalEvent<SurgeryTraumaPresentConditionComponent, SurgeryValidEvent>(OnTraumaPresentConditionValid);
        SubscribeLocalEvent<SurgeryBleedsPresentConditionComponent, SurgeryValidEvent>(OnBleedsPresentConditionValid);
        SubscribeLocalEvent<SurgeryMarkingConditionComponent, SurgeryValidEvent>(OnMarkingPresentValid);
        SubscribeLocalEvent<SurgeryStarvingPainConditionComponent, SurgeryValidEvent>(OnStarvingPainValid);
        //SubscribeLocalEvent<SurgeryRemoveLarvaComponent, SurgeryCompletedEvent>(OnRemoveLarva);

        InitializeSteps();
    }

    private void OnRoundRestartCleanup(RoundRestartCleanupEvent ev)
    {
        _surgeries.Clear();
    }

    private void OnTargetDoAfter(Entity<SurgeryTargetComponent> ent, ref SurgeryDoAfterEvent args)
    {
        if (!_timing.IsFirstTimePredicted)
            return;

        if (args.Cancelled
            || args.Handled
            || args.Target is not { } target
            || !IsSurgeryValid(ent, target, args.Surgery, args.Step, args.User, out var surgery, out var part, out var step)
            || !PreviousStepsComplete(ent, part, surgery, args.Step)
            || !CanPerformStep(args.User, ent, part, step, false))
        {
            Log.Warning($"{ToPrettyString(args.User)} tried to start invalid surgery.");
            return;
        }

        args.Repeat = (HasComp<SurgeryRepeatableStepComponent>(step) && !IsStepComplete(ent, part, args.Step, surgery));
        var ev = new SurgeryStepEvent(args.User, ent, part, GetTools(args.User), surgery);
        RaiseLocalEvent(step, ref ev);
        RefreshUI(ent);
    }

    private void OnCloseIncisionValid(Entity<SurgeryCloseIncisionConditionComponent> ent, ref SurgeryValidEvent args)
    {
        if (!HasComp<IncisionOpenComponent>(args.Part)
            && !HasComp<SkinRetractedComponent>(args.Part)
            && !HasComp<RibcageSawedComponent>(args.Part)
            && !HasComp<RibcageOpenComponent>(args.Part)
            && !HasComp<InternalBleedersClampedComponent>(args.Part)
            && !HasComp<BleedersClampedComponent>(args.Part))
        {
            args.Cancelled = true;
        }
    }

    private void OnWoundedValid(Entity<SurgeryWoundedConditionComponent> ent, ref SurgeryValidEvent args)
    {
        if (!TryComp(args.Part, out WoundableComponent? partWoundable)
            || _wounds.GetWoundableSeverityPoint(
                args.Part,
                partWoundable,
                ent.Comp.DamageGroup,
                healable: true) <= 0)
            args.Cancelled = true;
    }

    private void OnStarvingPainValid(Entity<SurgeryStarvingPainConditionComponent> ent, ref SurgeryValidEvent args)
    {
        if (!_consciousness.TryGetNerveSystem(args.Body, out var nerveSys)
            || !_pain.TryGetPainModifier(nerveSys.Value, args.Part, "Starving", out var modifier)
            || modifier.Value.PainType != PainType.Starving)
        {
            args.Cancelled = true;
        }
    }

    /*private void OnLarvaValid(Entity<SurgeryLarvaConditionComponent> ent, ref SurgeryValidEvent args)
    {
        if (!TryComp(args.Body, out VictimInfectedComponent? infected))
            args.Cancelled = true;

        // The larva has fully developed and surgery is now impossible
        if (infected != null && infected.SpawnedLarva != null)
            args.Cancelled = true;
    }*/

    private void OnComponentConditionValid(Entity<SurgeryComponentConditionComponent> ent, ref SurgeryValidEvent args)
    {
        var present = true;
        foreach (var reg in ent.Comp.Component.Values)
        {
            var compType = reg.Component.GetType();
            if (!HasComp(args.Part, compType))
                present = false;
        }

        if (ent.Comp.Inverse ? present : !present)
            args.Cancelled = true;
    }

    private void OnStatusEffectConditionValid(Entity<SurgeryBodyStatusEffectConditionComponent> ent, ref SurgeryValidEvent args)
    {
        var present = true;
        foreach (var effect in ent.Comp.StatusEffects)
        {
            if (!_statusEffects.HasStatusEffect(args.Body, effect))
                present = false;
        }

        if (ent.Comp.Inverse ? present : !present)
            args.Cancelled = true;
    }

    private void OnPartConditionValid(Entity<SurgeryPartConditionComponent> ent, ref SurgeryValidEvent args)
    {
        var valid = false;

        if (TryComp<BodyPartComponent>(args.Part, out var part))
        {
            var typeMatch = part.PartType == ent.Comp.Part;
            var symmetryMatch = ent.Comp.Symmetry == null || part.Symmetry == ent.Comp.Symmetry;
            valid = typeMatch && symmetryMatch;
        }
        else if (TryComp<OrganComponent>(args.Part, out var organ) && organ.Body == args.Body)
        {
            valid = _targeting.MatchesBodyPartType(args.Part, ent.Comp.Part, ent.Comp.Symmetry);
        }
        else
        {
            args.Cancelled = true;
            return;
        }

        if (ent.Comp.Inverse ? valid : !valid)
            args.Cancelled = true;
    }

    private void OnOrganConditionValid(Entity<SurgeryOrganConditionComponent> ent, ref SurgeryValidEvent args)
    {
        if (ent.Comp.Organ == null)
        {
            args.Cancelled = true;
            return;
        }

        foreach (var reg in ent.Comp.Organ.Values)
        {
            if (_body.TryGetInternalOrgansForHostPart(args.Body, args.Part, reg.Component.GetType(), out var organs)
                && organs.Count > 0)
            {
                if (ent.Comp.Inverse
                    && (!ent.Comp.Reattaching
                    || ent.Comp.Reattaching
                    && !organs.Any(organ => HasComp<OrganReattachedComponent>(organ.Id))))
                    args.Cancelled = true;
            }
            else if (!ent.Comp.Inverse)
                args.Cancelled = true;
        }
    }

    private void OnPartRemovedConditionValid(Entity<SurgeryPartRemovedConditionComponent> ent, ref SurgeryValidEvent args)
    {
        if (!_body.BodyExpectsReattachPart(args.Body, ent.Comp.Part, ent.Comp.Symmetry))
        {
            args.Cancelled = true;
            return;
        }

        if (!_body.TryGetWoundableTargetByType(args.Body, ent.Comp.Part, ent.Comp.Symmetry, out var partUid))
            return;

        if (!HasComp<OrganReattachedComponent>(partUid) && !HasComp<BodyPartReattachedComponent>(partUid))
            args.Cancelled = true;
    }

    private void OnOrganCategoryMissingConditionValid(Entity<SurgeryOrganCategoryMissingConditionComponent> ent, ref SurgeryValidEvent args)
    {
        var present = _organBody.TryGetOrganByCategory(args.Body, ent.Comp.Category, out _);

        if (ent.Comp.Inverse ? present : !present)
            args.Cancelled = true;
    }

    private void OnBothHumanLegsMissingValid(Entity<SurgeryBothHumanLegsMissingConditionComponent> ent, ref SurgeryValidEvent args)
    {
        if (_organBody.TryGetOrganByCategory(args.Body, "LegLeft", out _)
            || _organBody.TryGetOrganByCategory(args.Body, "LegRight", out _))
        {
            args.Cancelled = true;
        }
    }

    private void OnGraftAttachValid(Entity<SurgeryOrganGraftAttachComponent> ent, ref SurgeryValidEvent args)
    {
        if (ent.Comp.PrerequisiteCategory is { } prerequisite
            && !_organBody.TryGetOrganByCategory(args.Body, prerequisite, out _))
        {
            args.Cancelled = true;
            return;
        }

        if (!IsGraftAttachPending(args.Body, ent.Comp))
            args.Cancelled = true;
    }

    private void OnGraftDetachValid(Entity<SurgeryOrganGraftDetachComponent> ent, ref SurgeryValidEvent args)
    {
        if (!TryComp<OrganComponent>(args.Part, out var organ)
            || organ.Body != args.Body
            || organ.Category is not { } category
            || !SurgeryBodyPartMapping.IsArachneGraftCategory(category)
            || !SurgeryBodyPartMapping.CanDetachArachneGraftCategory(args.Body, category, _organBody))
        {
            args.Cancelled = true;
        }
    }

    private void OnArachneGraftOrganValid(Entity<SurgeryArachneGraftOrganConditionComponent> ent, ref SurgeryValidEvent args)
    {
        if (!TryComp<OrganComponent>(args.Part, out var organ) || organ.Body != args.Body)
            return;

        var isArachne = organ.Category is { } category
            && SurgeryBodyPartMapping.IsArachneGraftCategory(category);

        if (ent.Comp.Inverse ? isArachne : !isArachne)
            args.Cancelled = true;
    }

    private void OnPartPresentConditionValid(Entity<SurgeryPartPresentConditionComponent> ent, ref SurgeryValidEvent args)
    {
        if (args.Part == EntityUid.Invalid
            || !HasComp<BodyPartComponent>(args.Part) && !HasComp<OrganComponent>(args.Part))
            args.Cancelled = true;
    }

    private void OnTraumaPresentConditionValid(Entity<SurgeryTraumaPresentConditionComponent> ent, ref SurgeryValidEvent args)
    {
        if (args.Cancelled)
            return;

        // inverted = not cancelled (no trauma present), not inverted = cancelled (trauma present)
        args.Cancelled = !ent.Comp.Inverted;
        if (_trauma.HasWoundableTrauma(args.Part, ent.Comp.TraumaType))
            args.Cancelled = ent.Comp.Inverted;
        // if trauma is present and inverted - cancelled; if trauma is NOT present and inverted - not cancelled
        // if trauma is NOT present and NOT inverted = cancelled; if trauma is present and NOT inverted = not cancelled
    }

    private void OnBleedsPresentConditionValid(Entity<SurgeryBleedsPresentConditionComponent> ent, ref SurgeryValidEvent args)
    {
        if (args.Cancelled)
            return;

        // inverted = not cancelled; not inverted = cancelled
        args.Cancelled = !ent.Comp.Inverted;
        foreach (var woundEnt in _wounds.GetWoundableWounds(args.Part))
        {
            if (!TryComp<BleedInflicterComponent>(woundEnt, out var bleeds) || !bleeds.IsBleeding)
                continue;

            // if bleeds are present, and it's inverted... we cancel; Else we do not
            args.Cancelled = ent.Comp.Inverted;
            break;
        }
    }

    private void OnMarkingPresentValid(Entity<SurgeryMarkingConditionComponent> ent, ref SurgeryValidEvent args)
    {
        var hasMatch = _body.OrganHasMarking(args.Part, ent.Comp.MarkingCategory, ent.Comp.MatchString);
        args.Cancelled = ent.Comp.Inverse ? !hasMatch : hasMatch;
    }

    /*private void OnRemoveLarva(Entity<SurgeryRemoveLarvaComponent> ent, ref SurgeryCompletedEvent args)
    {
        RemCompDeferred<VictimInfectedComponent>(ent);
    }*/

    protected bool IsSurgeryValid(EntityUid body, EntityUid targetPart, EntProtoId surgery, EntProtoId stepId,
        EntityUid user, out Entity<SurgeryComponent> surgeryEnt, out EntityUid part, out EntityUid step)
    {
        surgeryEnt = default;
        part = default;
        step = default;

        if (!HasComp<SurgeryTargetComponent>(body) ||
            (TryComp<SurgeryTargetComponent>(body, out var bodySurgery) && !bodySurgery.CanBeOperatedOn) ||
            (TryComp<SurgeryTargetComponent>(user, out var userSurgery) && !userSurgery.CanOperate) ||
            !IsLyingDown(body, user) ||
            GetSingleton(surgery) is not { } surgeryEntId ||
            !TryComp(surgeryEntId, out SurgeryComponent? surgeryComp) ||
            !surgeryComp.Steps.Contains(stepId) ||
            GetSingleton(stepId) is not { } stepEnt
            || !IsValidSurgeryTarget(targetPart))
            return false;


        var ev = new SurgeryValidEvent(body, targetPart);
        if (_timing.IsFirstTimePredicted)
        {
            RaiseLocalEvent(stepEnt, ref ev);
            RaiseLocalEvent(surgeryEntId, ref ev);
        }

        if (ev.Cancelled)
            return false;

        surgeryEnt = (surgeryEntId, surgeryComp);
        part = targetPart;
        step = stepEnt;
        return true;
    }

    protected bool IsValidSurgeryTarget(EntityUid targetPart)
    {
        return HasComp<BodyPartComponent>(targetPart)
            || HasComp<OrganComponent>(targetPart)
            || HasComp<BodyComponent>(targetPart);
    }

    public EntityUid? GetSingleton(EntProtoId surgeryOrStep)
    {
        if (!_prototypes.HasIndex(surgeryOrStep))
            return null;

        // This (for now) assumes that surgery entity data remains unchanged between client
        // and server
        // if it does not you get the bullet
        if (!_surgeries.TryGetValue(surgeryOrStep, out var ent) || TerminatingOrDeleted(ent))
        {
            ent = Spawn(surgeryOrStep, MapCoordinates.Nullspace);
            _surgeries[surgeryOrStep] = ent;
        }

        return ent;
    }

    protected virtual List<EntityUid> GetTools(EntityUid surgeon)
    {
        var tools = new List<EntityUid>();
        foreach (var held in _hands.EnumerateHeld(surgeon))
        {
            tools.Add(held);

            if (TryComp<BodyComponent>(held, out var body) && body.Organs != null)
                tools.AddRange(body.Organs.ContainedEntities);
        }

        return tools;
    }

    public bool IsLyingDown(EntityUid entity, EntityUid user)
    {
        if (_standing.IsDown(entity))
            return true;

        if (TryComp(entity, out BuckleComponent? buckle) &&
            TryComp(buckle.BuckledTo, out StrapComponent? strap))
        {
            var rotation = strap.Rotation;
            if (rotation.GetCardinalDir() is Direction.West or Direction.East)
                return true;
        }

        _popup.PopupEntity(Loc.GetString("surgery-error-laying"), user, user);

        return false;
    }

    protected virtual void RefreshUI(EntityUid body)
    {
    }
}
