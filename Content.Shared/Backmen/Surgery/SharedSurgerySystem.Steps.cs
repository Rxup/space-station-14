using System.Diagnostics.CodeAnalysis;
using Content.Shared.Medical.Surgery.Conditions;
using Content.Shared.Medical.Surgery.Steps;
using Content.Shared.Humanoid;
using Content.Shared.Bed.Sleep;
using Content.Shared.Body.Part;
using Content.Shared.Body;
using Content.Shared.Buckle.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.DoAfter;
using Content.Shared.IdentityManagement;
using Content.Shared.Inventory;
using Content.Shared.Item;
using Content.Shared.Popups;
using Robust.Shared.Prototypes;
using System.Linq;
using Content.Shared.Backmen.Mood;
using Content.Shared.Backmen.Body.OrganRelations;
using Content.Shared.Backmen.Surgery.Body.Organs;
using Content.Shared.Backmen.Surgery.Effects.Step;
using Content.Shared.Backmen.Surgery.Steps;
using Content.Shared.Backmen.Surgery.Steps.Parts;
using Content.Shared.Backmen.Targeting;
using Content.Shared.Backmen.Surgery.Tools;
using Content.Shared.Backmen.Surgery.Traumas;
using Content.Shared.Backmen.Surgery.Traumas.Components;
using Content.Shared.Backmen.Surgery.Wounds.Components;
using Content.Shared.Backmen.Surgery.Pain;
using Content.Shared.FixedPoint;
using Content.Shared.Nutrition.Components;
using Robust.Shared.Utility;

namespace Content.Shared.Backmen.Surgery;

public abstract partial class SharedSurgerySystem
{
    private static readonly ProtoId<DamageTypePrototype> Poison = "Poison";
    private static readonly ProtoId<DamageGroupPrototype> Brute = "Brute";

    private void InitializeSteps()
    {
        SubscribeLocalEvent<SurgeryStepComponent, SurgeryStepEvent>(OnToolStep);
        SubscribeLocalEvent<SurgeryStepComponent, SurgeryStepCompleteCheckEvent>(OnToolCheck);
        SubscribeLocalEvent<SurgeryStepComponent, SurgeryCanPerformStepEvent>(OnToolCanPerform);

        //SubSurgery<SurgeryCutLarvaRootsStepComponent>(OnCutLarvaRootsStep, OnCutLarvaRootsCheck);

        /*  Abandon all hope ye who enter here. Now I am become shitcoder, the bloater of files.
            On a serious note, I really hate how much bloat this pattern of subscribing to a StepEvent and a CheckEvent
            creates in terms of readability. And while Check DOES only run on the server side, it's still annoying to parse through.*/

        SubSurgery<SurgeryTendWoundsEffectComponent>(OnTendWoundsStep, OnTendWoundsCheck);
        SubSurgery<SurgeryStepCavityEffectComponent>(OnCavityStep, OnCavityCheck);
        SubSurgery<SurgeryAddPartStepComponent>(OnAddPartStep, OnAddPartCheck);
        SubSurgery<SurgeryAffixPartStepComponent>(OnAffixPartStep, OnAffixPartCheck);
        SubSurgery<SurgeryRemovePartStepComponent>(OnRemovePartStep, OnRemovePartCheck);
        SubSurgery<SurgeryAddOrganStepComponent>(OnAddOrganStep, OnAddOrganCheck);
        SubSurgery<SurgeryRemoveOrganStepComponent>(OnRemoveOrganStep, OnRemoveOrganCheck);
        SubSurgery<SurgeryAffixOrganStepComponent>(OnAffixOrganStep, OnAffixOrganCheck);
        SubSurgery<SurgeryAddMarkingStepComponent>(OnAddMarkingStep, OnAddMarkingCheck);
        SubSurgery<SurgeryRemoveMarkingStepComponent>(OnRemoveMarkingStep, OnRemoveMarkingCheck);
        SubSurgery<SurgeryTraumaTreatmentStepComponent>(OnTraumaTreatmentStep, OnTraumaTreatmentCheck);
        SubSurgery<SurgeryBleedsTreatmentStepComponent>(OnBleedsTreatmentStep, OnBleedsTreatmentCheck);
        SubSurgery<SurgeryStepPainInflicterComponent>(OnPainInflicterStep, OnPainInflicterCheck);
        Subs.BuiEvents<SurgeryTargetComponent>(SurgeryUIKey.Key, subs =>
        {
            subs.Event<SurgeryStepChosenBuiMsg>(OnSurgeryTargetStepChosen);
        });
    }

    private bool CanReachSurgeryTarget(EntityUid user, EntityUid body)
    {
        // NOTE: Body parts / organs are frequently inside containers and may have Nullspace-ish transforms (e.g. 0,0).
        // Checking reach against the patient body avoids the classic "can't access organs during surgery" bug.
        return _interaction.InRangeAndAccessible((user, Transform(user)), (body, Transform(body)), range: 1.5f);
    }

    private void SubSurgery<TComp>(EntityEventRefHandler<TComp, SurgeryStepEvent> onStep,
        EntityEventRefHandler<TComp, SurgeryStepCompleteCheckEvent> onComplete) where TComp : IComponent
    {
        SubscribeLocalEvent(onStep);
        SubscribeLocalEvent(onComplete);
    }

    private void OnToolStep(Entity<SurgeryStepComponent> ent, ref SurgeryStepEvent args)
    {
        if (ent.Comp.Tool != null)
        {
            foreach (var reg in ent.Comp.Tool.Values)
            {
                if (!AnyHaveComp(args.Tools, reg.Component, out var tool, out _))
                    return;

                if (_net.IsServer &&
                    TryComp(tool, out SurgeryToolComponent? toolComp) &&
                    toolComp.EndSound != null)
                {
                    _audio.PlayEntity(toolComp.EndSound, args.User, tool);
                }
            }
        }



        if (ent.Comp.Add != null && _net.IsServer)
        {
            foreach (var reg in ent.Comp.Add.Values)
            {
                var compType = reg.Component.GetType();
                if (HasComp(args.Part, compType))
                    continue;
                AddComp(args.Part, _compFactory.GetComponent(compType));
            }
        }

        if (ent.Comp.Remove != null && _net.IsServer)
        {
            foreach (var reg in ent.Comp.Remove.Values)
            {
                RemComp(args.Part, reg.Component.GetType());
            }
        }

        if (ent.Comp.BodyAdd != null && _net.IsServer)
        {
            foreach (var reg in ent.Comp.BodyAdd.Values)
            {
                var compType = reg.Component.GetType();
                if (HasComp(args.Body, compType))
                    continue;
                AddComp(args.Body, _compFactory.GetComponent(compType));
            }
        }

        if (ent.Comp.BodyRemove != null && _net.IsServer)
        {
            foreach (var reg in ent.Comp.BodyRemove.Values)
            {
                RemComp(args.Body, reg.Component.GetType());
            }
        }

        if (ent.Comp.BodyStatusEffectAdd != null)
        {
            foreach (var effect in ent.Comp.BodyStatusEffectAdd)
            {
                _statusEffects.TrySetStatusEffectDuration(args.Body, effect);
            }
        }

        if (ent.Comp.BodyStatusEffectRemove != null)
        {
            foreach (var effect in ent.Comp.BodyStatusEffectRemove)
            {
                _statusEffects.TryRemoveStatusEffect(args.Body, effect);
            }
        }

        if (!HasComp<ForcedSleepingStatusEffectComponent>(args.Body))
            RaiseLocalEvent(args.Body, new MoodEffectEvent("SurgeryPain"));

        if (!_inventory.TryGetSlotEntity(args.User, "gloves", out var _)
            || !_inventory.TryGetSlotEntity(args.User, "mask", out var _))
        {
            if (!HasComp<SanitizedComponent>(args.User))
            {
                var sepsis = new DamageSpecifier(_prototypes.Index<DamageTypePrototype>(Poison), 5);
                var ev = new SurgeryStepDamageEvent(args.User, args.Body, args.Part, args.Surgery, sepsis, 0.5f);
                RaiseLocalEvent(args.Body, ref ev);
            }
        }
    }

    private void OnToolCheck(Entity<SurgeryStepComponent> ent, ref SurgeryStepCompleteCheckEvent args)
    {
        // Lord this function is fucking bloated now. Need to clean it up so its less spammy.
        if (ent.Comp.Add != null)
        {
            foreach (var reg in ent.Comp.Add.Values)
            {
                if (!HasComp(args.Part, reg.Component.GetType()))
                {
                    args.Cancelled = true;
                    return;
                }
            }
        }

        if (ent.Comp.Remove != null)
        {
            foreach (var reg in ent.Comp.Remove.Values)
            {
                if (HasComp(args.Part, reg.Component.GetType()))
                {
                    args.Cancelled = true;
                    return;
                }
            }
        }

        if (ent.Comp.BodyAdd != null)
        {
            foreach (var reg in ent.Comp.BodyAdd.Values)
            {
                if (!HasComp(args.Body, reg.Component.GetType()))
                {
                    args.Cancelled = true;
                    return;
                }
            }
        }

        if (ent.Comp.BodyRemove != null)
        {
            foreach (var reg in ent.Comp.BodyRemove.Values)
            {
                if (HasComp(args.Body, reg.Component.GetType()))
                {
                    args.Cancelled = true;
                    return;
                }
            }
        }

        if (ent.Comp.BodyStatusEffectAdd != null)
        {
            foreach (var effect in ent.Comp.BodyStatusEffectAdd)
            {
                if (!_statusEffects.HasStatusEffect(args.Body, effect))
                {
                    args.Cancelled = true;
                    return;
                }
            }
        }

        if (ent.Comp.BodyStatusEffectRemove != null)
        {
            foreach (var effect in ent.Comp.BodyStatusEffectRemove)
            {
                if (_statusEffects.HasStatusEffect(args.Body, effect))
                {
                    args.Cancelled = true;
                    return;
                }
            }
        }
    }

    private void OnToolCanPerform(Entity<SurgeryStepComponent> ent, ref SurgeryCanPerformStepEvent args)
    {
        if (HasComp<SurgeryOperatingTableConditionComponent>(ent))
        {
            if (!TryComp(args.Body, out BuckleComponent? buckle) ||
                !HasComp<OperatingTableComponent>(buckle.BuckledTo))
            {
                args.Invalid = StepInvalidReason.NeedsOperatingTable;
                return;
            }
        }

        if (args.TargetSlots != SlotFlags.NONE
            && _inventory.TryGetContainerSlotEnumerator(args.Body, out var containerSlotEnumerator, args.TargetSlots))
        {
            while (containerSlotEnumerator.MoveNext(out var containerSlot))
            {
                if (!containerSlot.ContainedEntity.HasValue)
                    continue;

                args.Invalid = StepInvalidReason.Armor;
                args.Popup = Loc.GetString("surgery-ui-window-steps-error-armor");
                return;
            }
        }

        RaiseLocalEvent(args.Body, ref args);

        if (args.Invalid != StepInvalidReason.None)
            return;

        if (ent.Comp.Tool != null)
        {
            args.ValidTools ??= new Dictionary<EntityUid, float>();

            foreach (var reg in ent.Comp.Tool.Values)
            {
                if (!AnyHaveComp(args.Tools, reg.Component, out var tool, out var speed))
                {
                    args.Invalid = StepInvalidReason.MissingTool;

                    if (reg.Component is ISurgeryToolComponent required)
                        args.Popup = $"You need {required.ToolName} to perform this step!";

                    return;
                }

                args.ValidTools[tool] = speed;
            }
        }
    }

    private void OnTendWoundsStep(Entity<SurgeryTendWoundsEffectComponent> ent, ref SurgeryStepEvent args)
    {
        if (_wounds.GetWoundableSeverityPoint(
                args.Part,
                damageGroup: ent.Comp.MainGroup,
                healable: true) <= 0)
            return;

        // Right now the bonus is based off the body's total damage, maybe we could make it based off each part in the future.
        var bonus = ent.Comp.HealMultiplier * _wounds.GetWoundableSeverityPoint(args.Part, damageGroup: ent.Comp.MainGroup);
        if (_mobState.IsDead(args.Body))
            bonus *= 1.2;

        var adjustedDamage = new DamageSpecifier(ent.Comp.Damage);
        var group = _prototypes.Index<DamageGroupPrototype>(ent.Comp.MainGroup);
        foreach (var type in group.DamageTypes)
        {
            adjustedDamage.DamageDict[type] -= bonus;
        }

        var ev = new SurgeryStepDamageEvent(args.User, args.Body, args.Part, args.Surgery, adjustedDamage, 1.5f);
        RaiseLocalEvent(args.Body, ref ev);
    }

    private void OnTendWoundsCheck(Entity<SurgeryTendWoundsEffectComponent> ent, ref SurgeryStepCompleteCheckEvent args)
    {
        if (_wounds.HasDamageOfGroup(args.Part, ent.Comp.MainGroup, true))
            args.Cancelled = true;
    }

    /*private void OnCutLarvaRootsStep(Entity<SurgeryCutLarvaRootsStepComponent> ent, ref SurgeryStepEvent args)
    {
        if (TryComp(args.Body, out VictimInfectedComponent? infected) &&
            infected.BurstAt > _timing.CurTime &&
            infected.SpawnedLarva == null)
        {
            infected.RootsCut = true;
        }
    }

    private void OnCutLarvaRootsCheck(Entity<SurgeryCutLarvaRootsStepComponent> ent, ref SurgeryStepCompleteCheckEvent args)
    {
        if (!TryComp(args.Body, out VictimInfectedComponent? infected) || !infected.RootsCut)
            args.Cancelled = true;

        // The larva has fully developed and surgery is now impossible
        // TODO: Surgery should still be possible, but the fully developed larva should escape while also saving the hosts life
        if (infected != null && infected.SpawnedLarva != null)
            args.Cancelled = true;
    }*/

    private void OnCavityStep(Entity<SurgeryStepCavityEffectComponent> ent, ref SurgeryStepEvent args)
    {
        if (!TryGetTorsoCavityEntity(args.Part, out var cavityEntity)
            || !_itemSlotsSystem.TryGetSlot(cavityEntity, SurgeryStepCavityEffectComponent.SlotId, out var slot))
            return;

        var activeHandEntity = _hands.EnumerateHeld(args.User).FirstOrDefault();
        if (ent.Comp.Action == "Insert"
            && activeHandEntity != default
            && TryComp(activeHandEntity, out ItemComponent? itemComp)
            && (itemComp.Size.Id == "Tiny" || itemComp.Size.Id == "Small"))
        {
            _itemSlotsSystem.TryInsert(cavityEntity, slot, activeHandEntity, args.User);
        }
        else if (ent.Comp.Action == "Remove")
        {
            _itemSlotsSystem.TryEjectToHands(cavityEntity, slot, args.User);
        }
    }

    private void OnCavityCheck(Entity<SurgeryStepCavityEffectComponent> ent, ref SurgeryStepCompleteCheckEvent args)
    {
        if (!TryGetTorsoCavityEntity(args.Part, out var cavityEntity)
            || !_itemSlotsSystem.TryGetSlot(cavityEntity, SurgeryStepCavityEffectComponent.SlotId, out var slot))
        {
            args.Cancelled = true;
            return;
        }

        var hasItem = slot.HasItem;
        if (ent.Comp.Action == "Insert" && !hasItem
            || ent.Comp.Action == "Remove" && hasItem)
            args.Cancelled = true;
    }

    private bool TryGetTorsoCavityEntity(EntityUid part, out EntityUid cavityEntity)
    {
        if (TryComp<OrganComponent>(part, out var organ) && organ.Category == "Torso")
        {
            cavityEntity = part;
            return true;
        }

        if (TryComp<BodyPartComponent>(part, out var bodyPart) && bodyPart.PartType == BodyPartType.Chest)
        {
            cavityEntity = part;
            return true;
        }

        cavityEntity = default;
        return false;
    }

    private void OnAddPartStep(Entity<SurgeryAddPartStepComponent> ent, ref SurgeryStepEvent args)
    {
        if (TryComp(args.Surgery, out SurgeryOrganGraftAttachComponent? graftComp))
        {
            TryGraftOrganAttach(args.Body, args.Surgery, graftComp, args.Tools);
            return;
        }

        if (!TryComp(args.Surgery, out SurgeryPartRemovedConditionComponent? removedComp))
            return;

        foreach (var tool in args.Tools)
        {
            var attachOrgan = tool;
            EntityUid? bundleEnt = null;

            if (_detachedBodies.TryGetMatchingOrgan(tool, removedComp.Part, removedComp.Symmetry, out var bundledOrgan))
            {
                attachOrgan = bundledOrgan;
                bundleEnt = tool;
            }
            else if (TryComp<OrganComponent>(attachOrgan, out var attachOrganComp)
                && attachOrganComp.Body is { } host
                && HasComp<BkmDetachedBodyComponent>(host))
            {
                bundleEnt = host;
            }

            if (TryComp<OrganComponent>(attachOrgan, out var organ)
                && organ.Body != args.Body
                && _targeting.MatchesBodyPartType(attachOrgan, removedComp.Part, removedComp.Symmetry)
                && !(organ.Category is { } attachCategory
                    && (SurgeryBodyPartMapping.IsSpiderLegCategory(attachCategory)
                        || SurgeryBodyPartMapping.IsHumanLegOrFootCategory(attachCategory)
                            && _body.BodyHasArachneOrgan(args.Body)))
                && _body.InsertOrganIntoBody(args.Body, attachOrgan))
            {
                if (bundleEnt is { } bundle)
                    TransferDetachedSubtreeOrgans(args.Body, attachOrgan, bundle);

                _wounds.RestoreWoundableAfterReattachment(attachOrgan);
                EnsureComp<OrganReattachedComponent>(attachOrgan);
                if (TryComp<BodyComponent>(args.Body, out var bodyComp))
                    _organRelations.WireRelationships((args.Body, bodyComp));
                RefreshBodyAppearance(args.Body, attachOrgan);
                _wounds.RefreshBodyTargetingStatus(args.Body);

                if (bundleEnt is { } emptyBundle)
                    TryCleanupEmptyDetachedBody(emptyBundle);

                return;
            }

            if (TryComp(tool, out BodyPartComponent? partComp)
                && partComp.PartType == removedComp.Part
                && (removedComp.Symmetry == null || partComp.Symmetry == removedComp.Symmetry)
                && !(_body.BodyHasArachneOrgan(args.Body)
                    && SurgeryBodyPartMapping.TryGetCategory(removedComp.Part, removedComp.Symmetry, out var blockedCategory)
                    && SurgeryBodyPartMapping.IsHumanLegOrFootCategory(blockedCategory)))
            {
                var slotName = removedComp.Symmetry != null
                    ? $"{removedComp.Symmetry?.ToString().ToLower()} {removedComp.Part.ToString().ToLower()}"
                    : removedComp.Part.ToString().ToLower();
                _body.TryCreatePartSlot(args.Part, slotName, partComp.PartType, out _, partComp.Symmetry);
                _body.AttachPart(args.Part, slotName, tool);
                EnsureComp<BodyPartReattachedComponent>(tool);
            }
        }
    }

    /// <summary>
    /// Moves internal organs still stored in a detached bundle into the patient when reattaching a limb.
    /// </summary>
    private void TransferDetachedSubtreeOrgans(EntityUid patient, EntityUid rootOrgan, EntityUid bundleEnt)
    {
        foreach (var child in _organRelation.AllChildren(rootOrgan))
        {
            if (!TryComp<OrganComponent>(child, out var organ) || organ.Body != bundleEnt)
                continue;

            _body.InsertOrganIntoBody(patient, child);
        }
    }

    private void TryCleanupEmptyDetachedBody(EntityUid bundleEnt)
    {
        if (!_net.IsServer
            || !TryComp<BodyComponent>(bundleEnt, out var body)
            || body.Organs == null
            || body.Organs.Count > 0)
        {
            return;
        }

        QueueDel(bundleEnt);
    }

    private void OnAffixPartStep(Entity<SurgeryAffixPartStepComponent> ent, ref SurgeryStepEvent args)
    {
        if (TryGetGraftAttachOrgan(args.Body, args.Surgery, out var partId))
        {
            _wounds.TryHealWoundsOnWoundable(partId, 12f, out _, damageGroup: _prototypes.Index<DamageGroupPrototype>(Brute));
            RemComp<OrganReattachedComponent>(partId);
            RemComp<BodyPartReattachedComponent>(partId);
            RefreshBodyAppearance(args.Body, partId);
            _wounds.RefreshBodyTargetingStatus(args.Body);
            return;
        }

        if (!TryComp(args.Surgery, out SurgeryPartRemovedConditionComponent? removedComp))
            return;

        if (!_targeting.TryGetEntityByBodyPartType(args.Body, removedComp.Part, removedComp.Symmetry, out var legacyPartId))
            return;

        _wounds.TryHealWoundsOnWoundable(legacyPartId, 12f, out _, damageGroup: _prototypes.Index<DamageGroupPrototype>(Brute));

        RemComp<OrganReattachedComponent>(legacyPartId);
        RemComp<BodyPartReattachedComponent>(legacyPartId);
        RefreshBodyAppearance(args.Body, legacyPartId);
        _wounds.RefreshBodyTargetingStatus(args.Body);
    }

    private void OnAffixPartCheck(Entity<SurgeryAffixPartStepComponent> ent, ref SurgeryStepCompleteCheckEvent args)
    {
        if (TryComp(args.Surgery, out SurgeryOrganGraftAttachComponent? graftComp))
        {
            if (!TryGetGraftAttachOrgan(args.Body, args.Surgery, out var partId))
            {
                args.Cancelled = true;
                return;
            }

            if (HasComp<OrganReattachedComponent>(partId) || HasComp<BodyPartReattachedComponent>(partId))
                args.Cancelled = true;
            return;
        }

        if (!TryComp(args.Surgery, out SurgeryPartRemovedConditionComponent? removedComp))
            return;

        if (!_targeting.TryGetEntityByBodyPartType(args.Body, removedComp.Part, removedComp.Symmetry, out var legacyPartId))
            return;

        if (HasComp<OrganReattachedComponent>(legacyPartId) || HasComp<BodyPartReattachedComponent>(legacyPartId))
            args.Cancelled = true;
    }

    private void OnAddPartCheck(Entity<SurgeryAddPartStepComponent> ent, ref SurgeryStepCompleteCheckEvent args)
    {
        if (TryComp(args.Surgery, out SurgeryOrganGraftAttachComponent? graftComp))
        {
            if (IsGraftAttachPending(args.Body, graftComp))
                args.Cancelled = true;
            return;
        }

        if (!TryComp(args.Surgery, out SurgeryPartRemovedConditionComponent? removedComp)
            || !_targeting.TryGetEntityByBodyPartType(args.Body, removedComp.Part, removedComp.Symmetry, out var attachedPart)
            || TryComp<OrganComponent>(attachedPart, out var attachedOrgan)
                && attachedOrgan.Category is { } attachedCategory
                && SurgeryBodyPartMapping.IsSpiderLegCategory(attachedCategory))
            args.Cancelled = true;
    }

    private bool IsGraftAttachPending(EntityUid body, SurgeryOrganGraftAttachComponent graft)
    {
        if (graft.RequiredCategory is { } requiredCategory)
            return !_organBody.TryGetOrganByCategory(body, requiredCategory, out _);

        if (graft.SpiderLegSide is { } side)
            return TryGetFirstMissingSpiderLegSlot(body, side, out _);

        return false;
    }

    private bool TryGetFirstMissingSpiderLegSlot(
        EntityUid body,
        BodyPartSymmetry side,
        out ProtoId<OrganCategoryPrototype> slot)
    {
        slot = default;
        var slots = side == BodyPartSymmetry.Left
            ? SurgeryBodyPartMapping.SpiderLegLeftSlots
            : SurgeryBodyPartMapping.SpiderLegRightSlots;

        foreach (var candidate in slots)
        {
            if (_organBody.TryGetOrganByCategory(body, candidate, out _))
                continue;

            slot = candidate;
            return true;
        }

        return false;
    }

    private bool TryGetGraftAttachOrgan(EntityUid body, EntityUid surgery, out EntityUid organ)
    {
        organ = default;

        if (!TryComp(surgery, out SurgeryOrganGraftAttachComponent? graft))
            return false;

        if (graft.RequiredCategory is { } requiredCategory
            && _organBody.TryGetOrganByCategory(body, requiredCategory, out var requiredOrgan))
        {
            organ = requiredOrgan;
            return true;
        }

        if (graft.SpiderLegSide is { } side)
        {
            var slots = side == BodyPartSymmetry.Left
                ? SurgeryBodyPartMapping.SpiderLegLeftSlots
                : SurgeryBodyPartMapping.SpiderLegRightSlots;

            foreach (var slot in slots)
            {
                if (_organBody.TryGetOrganByCategory(body, slot, out var legOrgan))
                {
                    organ = legOrgan;
                    return true;
                }
            }
        }

        return false;
    }

    private void TryGraftOrganAttach(EntityUid body, EntityUid surgery, SurgeryOrganGraftAttachComponent graft, IReadOnlyList<EntityUid> tools)
    {
        foreach (var tool in tools)
        {
            var attachOrgan = tool;
            EntityUid? bundleEnt = null;

            if (!TryComp<OrganComponent>(attachOrgan, out var organComp) || organComp.Body == body)
                continue;

            ProtoId<OrganCategoryPrototype>? targetCategory = null;

            if (graft.SpiderLegSide is { } side)
            {
                if (organComp.Category is not { } legCategory
                    || !SurgeryBodyPartMapping.IsSpiderLegCategory(legCategory))
                    continue;

                if (!TryGetFirstMissingSpiderLegSlot(body, side, out var slot))
                    continue;

                targetCategory = slot;
                _organBody.SetOrganCategory(attachOrgan, slot);
            }
            else if (graft.RequiredCategory is { } required)
            {
                if (organComp.Category != required)
                    continue;

                targetCategory = required;
            }
            else
            {
                continue;
            }

            if (organComp.Body is { } host && HasComp<BkmDetachedBodyComponent>(host))
                bundleEnt = host;

            if (targetCategory is not { } category
                || _organBody.TryGetOrganByCategory(body, category, out _)
                || !_body.InsertOrganIntoBody(body, attachOrgan))
                continue;

            if (bundleEnt is { } bundle)
                TransferDetachedSubtreeOrgans(body, attachOrgan, bundle);

            _wounds.RestoreWoundableAfterReattachment(attachOrgan);
            EnsureComp<OrganReattachedComponent>(attachOrgan);
            if (TryComp<BodyComponent>(body, out var bodyComp))
            {
                _organRelations.WireRelationships((body, bodyComp));
                _organRelations.WireGraftRelationships((body, bodyComp));
            }

            RefreshBodyAppearance(body, attachOrgan);
            _wounds.RefreshBodyTargetingStatus(body);

            if (bundleEnt is { } emptyBundle)
                TryCleanupEmptyDetachedBody(emptyBundle);

            return;
        }
    }

    private void OnRemovePartStep(Entity<SurgeryRemovePartStepComponent> ent, ref SurgeryStepEvent args)
    {
        if (TryComp<OrganComponent>(args.Part, out var organ) && organ.Body == args.Body)
        {
            AmputateNubodyExternalOrgan(args.Body, args.User, args.Part);
            return;
        }

        if (!TryComp(args.Part, out BodyPartComponent? partComp)
            || partComp.Body != args.Body)
            return;

        if (!_body.TryGetParentBodyPart(args.Part, out var parentPart, out _))
            return;

        _wounds.AmputateWoundableSafely(parentPart.Value, args.Part);
        _hands.TryPickupAnyHand(args.User, args.Part);
    }

    private void OnRemovePartCheck(Entity<SurgeryRemovePartStepComponent> ent, ref SurgeryStepCompleteCheckEvent args)
    {
        if (TryComp<OrganComponent>(args.Part, out var organ))
        {
            if (organ.Body == args.Body)
                args.Cancelled = true;
            return;
        }

        if (!TryComp(args.Part, out BodyPartComponent? partComp)
            || partComp.Body == args.Body)
            args.Cancelled = true;
    }

    /// <summary>
    /// Resolves the woundable to receive transferred damage when amputating a nubody external organ.
    /// </summary>
    private bool TryGetAmputationParentWoundable(
        EntityUid body,
        EntityUid part,
        [NotNullWhen(true)] out EntityUid parentWoundable)
    {
        parentWoundable = default;

        if (!_targeting.TryGetEntityByBodyPartType(body, BodyPartType.Chest, null, out parentWoundable))
            return false;

        var partType = _targeting.GetBodyPartType(part);
        var symmetry = _targeting.GetBodyPartSymmetry(part);

        if (partType is not { } type)
            return true;

        parentWoundable = (type, symmetry) switch
        {
            (BodyPartType.Hand, BodyPartSymmetry.Left)
                when _targeting.TryGetEntityByBodyPartType(body, BodyPartType.Arm, BodyPartSymmetry.Left, out var arm) => arm,
            (BodyPartType.Hand, BodyPartSymmetry.Right)
                when _targeting.TryGetEntityByBodyPartType(body, BodyPartType.Arm, BodyPartSymmetry.Right, out var arm) => arm,
            (BodyPartType.Foot, BodyPartSymmetry.Left)
                when _targeting.TryGetEntityByBodyPartType(body, BodyPartType.Leg, BodyPartSymmetry.Left, out var leg) => leg,
            (BodyPartType.Foot, BodyPartSymmetry.Right)
                when _targeting.TryGetEntityByBodyPartType(body, BodyPartType.Leg, BodyPartSymmetry.Right, out var leg) => leg,
            _ => parentWoundable,
        };

        return true;
    }

    private void AmputateNubodyExternalOrgan(EntityUid body, EntityUid user, EntityUid part)
    {
        if (TryComp<OrganComponent>(part, out var organ) && organ.Category is { } organCategory)
            TransferAmputationSurgeryState(body, part, organCategory);

        RemoveExternalOrgan(body, user, part);
    }

    private void RemoveExternalOrgan(EntityUid body, EntityUid user, EntityUid organEnt)
    {
        if (!TryComp<OrganComponent>(organEnt, out var organ) || organ.Body != body)
            return;

        if (TryGetAmputationParentWoundable(body, organEnt, out var parentWoundable))
            _wounds.AmputateWoundableSafely(parentWoundable, organEnt);

        TryPickupAmputatedOrgan(user, organEnt);
    }

    private void TryPickupAmputatedOrgan(EntityUid user, EntityUid organEnt)
    {
        if (TryComp<OrganComponent>(organEnt, out var organ)
            && organ.Body is { } bundle
            && HasComp<BkmDetachedBodyComponent>(bundle)
            && _hands.TryPickupAnyHand(user, bundle))
        {
            return;
        }

        if (_hands.TryPickupAnyHand(user, organEnt))
            return;

        if (TryComp<OrganComponent>(organEnt, out organ)
            && organ.Body is { } holder
            && HasComp<ItemComponent>(holder))
            _hands.TryPickupAnyHand(user, holder);
    }

    private void TransferAmputationSurgeryState(
        EntityUid body,
        EntityUid part,
        ProtoId<OrganCategoryPrototype> category)
    {
        if (!TryGetSurgeryStateTransferTarget(body, category, out var target) || target == part)
            return;

        CopySurgeryStateComponent<IncisionOpenComponent>(part, target);
        CopySurgeryStateComponent<SkinRetractedComponent>(part, target);
        CopySurgeryStateComponent<RibcageSawedComponent>(part, target);
        CopySurgeryStateComponent<RibcageOpenComponent>(part, target);
        CopySurgeryStateComponent<InternalBleedersClampedComponent>(part, target);
        CopySurgeryStateComponent<BleedersClampedComponent>(part, target);
        CopySurgeryStateComponent<BodyPartSawedComponent>(part, target);
    }

    private void CopySurgeryStateComponent<T>(EntityUid from, EntityUid to) where T : IComponent, new()
    {
        if (!HasComp<T>(from) || HasComp<T>(to))
            return;

        EnsureComp<T>(to);
    }

    private bool TryGetSurgeryStateTransferTarget(
        EntityUid body,
        ProtoId<OrganCategoryPrototype> category,
        [NotNullWhen(true)] out EntityUid target)
    {
        target = default;

        if (!SurgeryBodyPartMapping.TryGetBodyPartType(category, out var type, out var symmetry))
            return false;

        var sym = symmetry == BodyPartSymmetry.None ? null : symmetry;

        if (type == BodyPartType.Hand
            && _targeting.TryGetEntityByBodyPartType(body, BodyPartType.Arm, sym, out target))
            return true;

        if (type == BodyPartType.Foot
            && _targeting.TryGetEntityByBodyPartType(body, BodyPartType.Leg, sym, out target))
            return true;

        return _targeting.TryGetEntityByBodyPartType(body, BodyPartType.Chest, null, out target);
    }

    private void OnAddOrganStep(Entity<SurgeryAddOrganStepComponent> ent, ref SurgeryStepEvent args)
    {
        if (!TryComp(args.Surgery, out SurgeryOrganConditionComponent? organComp)
            || organComp.Organ == null)
            return;

        var firstOrgan = organComp.Organ.Values.FirstOrDefault();
        if (firstOrgan == default)
            return;

        foreach (var tool in args.Tools)
        {
            if (!HasComp(tool, firstOrgan.Component.GetType())
                || !TryComp<OrganComponent>(tool, out var insertedOrgan))
                continue;

            if (insertedOrgan.Body == args.Body)
                continue;

            if (!_body.InsertOrganIntoBody(args.Body, tool))
                continue;

            EnsureComp<OrganReattachedComponent>(tool);
            if (TryComp<BodyComponent>(args.Body, out var bodyComp))
                _organRelations.WireRelationships((args.Body, bodyComp));

            if (_body.TrySetOrganUsed(tool, true, insertedOrgan)
                && insertedOrgan.OriginalBody != args.Body)
            {
                var ev = new SurgeryStepDamageChangeEvent(args.User, args.Body, args.Part, ent);
                RaiseLocalEvent(ent, ref ev);
            }

            break;
        }
    }

    private void OnAddOrganCheck(Entity<SurgeryAddOrganStepComponent> ent, ref SurgeryStepCompleteCheckEvent args)
    {
        if (!TryComp<SurgeryOrganConditionComponent>(args.Surgery, out var organComp)
            || organComp.Organ is null)
            return;

        var lookup = args.Part;

        foreach (var reg in organComp.Organ.Values)
        {
            if (!_body.TryGetInternalOrgansForHostPart(args.Body, lookup, reg.Component.GetType(), out var _))
                args.Cancelled = true;
        }
    }

    private void OnAffixOrganStep(Entity<SurgeryAffixOrganStepComponent> ent, ref SurgeryStepEvent args)
    {
        if (!TryComp(args.Surgery, out SurgeryOrganConditionComponent? removedOrganComp)
            || removedOrganComp.Organ == null
            || !removedOrganComp.Reattaching)
            return;

        var lookup = args.Part;

        foreach (var reg in removedOrganComp.Organ.Values)
        {
            _body.TryGetInternalOrgansForHostPart(args.Body, lookup, reg.Component.GetType(), out var organs);
            if (organs != null && organs.Count > 0)
            {
                RemComp<OrganReattachedComponent>(organs[0].Id);
                RefreshBodyAppearance(args.Body, organs[0].Id);
            }
        }

        if (TryComp<BodyComponent>(args.Body, out var bodyComp))
            _organRelations.WireRelationships((args.Body, bodyComp));
    }

    private void OnAffixOrganCheck(Entity<SurgeryAffixOrganStepComponent> ent, ref SurgeryStepCompleteCheckEvent args)
    {
        if (!TryComp(args.Surgery, out SurgeryOrganConditionComponent? removedOrganComp)
            || removedOrganComp.Organ == null
            || !removedOrganComp.Reattaching)
            return;

        var lookup = args.Part;

        foreach (var reg in removedOrganComp.Organ.Values)
        {
            _body.TryGetInternalOrgansForHostPart(args.Body, lookup, reg.Component.GetType(), out var organs);
            if (organs != null
                && organs.Count > 0
                && organs.Any(organ => HasComp<OrganReattachedComponent>(organ.Id)))
                args.Cancelled = true;
        }
    }

    private void OnRemoveOrganStep(Entity<SurgeryRemoveOrganStepComponent> ent, ref SurgeryStepEvent args)
    {
        if (!TryComp<SurgeryOrganConditionComponent>(args.Surgery, out var organComp)
            || organComp.Organ == null)
            return;

        var lookup = args.Part;

        foreach (var reg in organComp.Organ.Values)
        {
            _body.TryGetInternalOrgansForHostPart(args.Body, lookup, reg.Component.GetType(), out var organs);
            if (organs != null && organs.Count > 0)
            {
                _body.RemoveOrgan(organs[0].Id, organs[0].Organ);
                _hands.TryPickupAnyHand(args.User, organs[0].Id);
            }
        }
    }

    private void OnRemoveOrganCheck(Entity<SurgeryRemoveOrganStepComponent> ent, ref SurgeryStepCompleteCheckEvent args)
    {
        if (!TryComp<SurgeryOrganConditionComponent>(args.Surgery, out var organComp)
            || organComp.Organ == null)
            return;

        var lookup = args.Part;

        foreach (var reg in organComp.Organ.Values)
        {
            if (_body.TryGetInternalOrgansForHostPart(args.Body, lookup, reg.Component.GetType(), out var organs)
                && organs != null
                && organs.Count > 0)
            {
                args.Cancelled = true;
                return;
            }
        }
    }

    // TODO: Refactor bodies to include ears as a prototype instead of doing whatever the hell this is.
    private void OnAddMarkingStep(Entity<SurgeryAddMarkingStepComponent> ent, ref SurgeryStepEvent args)
    {
        if (!TryComp(args.Body, out HumanoidProfileComponent? bodyAppearance)
            || ent.Comp.Organ == null)
            return;

        var organType = ent.Comp.Organ.Values.FirstOrDefault();
        if (organType == default)
            return;

        var markingCategory = MarkingCategoriesConversion.FromHumanoidVisualLayers(ent.Comp.MarkingCategory);
        foreach (var tool in args.Tools)
        {
            if (TryComp(tool, out MarkingContainerComponent? markingComp)
                && HasComp(tool, organType.Component.GetType()))
            {
                _body.ModifyMarkings(args.Body, args.Part, bodyAppearance, ent.Comp.MarkingCategory, markingComp.Marking);

                if (ent.Comp.Accent != null
                    && ent.Comp.Accent.Values.FirstOrDefault() is { } accent)
                {
                    var compType = accent.Component.GetType();
                    if (!HasComp(args.Body, compType))
                        AddComp(args.Body, _compFactory.GetComponent(compType));
                }

                QueueDel(tool);
            }
        }

    }

    private void OnAddMarkingCheck(Entity<SurgeryAddMarkingStepComponent> ent, ref SurgeryStepCompleteCheckEvent args)
    {
        if (!TryComp(args.Body, out HumanoidProfileComponent? _))
            args.Cancelled = true;
    }

    private void OnRemoveMarkingStep(Entity<SurgeryRemoveMarkingStepComponent> ent, ref SurgeryStepEvent args)
    {

    }

    private void OnRemoveMarkingCheck(Entity<SurgeryRemoveMarkingStepComponent> ent, ref SurgeryStepCompleteCheckEvent args)
    {

    }

    private void OnTraumaTreatmentStep(Entity<SurgeryTraumaTreatmentStepComponent> ent, ref SurgeryStepEvent args)
    {
        var healAmount = ent.Comp.Amount;
        switch (ent.Comp.TraumaType)
        {
            case TraumaType.OrganDamage:
                foreach (var organ in _body.GetBodyOrgans(args.Body))
                {
                    foreach (var modifier in organ.Component.IntegrityModifiers)
                    {
                        var delta = healAmount - modifier.Value;
                        if (delta > 0)
                        {
                            healAmount -= modifier.Value;
                            _trauma.TryRemoveOrganDamageModifier(
                                organ.Id,
                                modifier.Key.Item2,
                                modifier.Key.Item1,
                                organ.Component);
                        }
                        else
                        {
                            _trauma.TryChangeOrganDamageModifier(
                                organ.Id,
                                -healAmount,
                                modifier.Key.Item2,
                                modifier.Key.Item1,
                                organ.Component);
                            break;
                        }
                    }
                }

                break;

            case TraumaType.BoneDamage:
                if (!TryComp<WoundableComponent>(args.Part, out var woundable))
                    return;

                var bone = woundable.Bone.ContainedEntities.FirstOrNull();
                if (bone == null || !TryComp<BoneComponent>(bone, out var boneComp))
                    return;

                _trauma.ApplyDamageToBone(bone.Value, -healAmount, boneComp);
                break;

            case TraumaType.Dismemberment:
                if (_trauma.TryGetWoundableTrauma(args.Part, out var traumas, TraumaType.Dismemberment))
                {
                    foreach (var trauma in traumas)
                    {
                        // yeah, that simple
                        _trauma.RemoveTrauma(trauma);
                    }
                }
                break;
        }
    }

    private void OnTraumaTreatmentCheck(Entity<SurgeryTraumaTreatmentStepComponent> ent, ref SurgeryStepCompleteCheckEvent args)
    {
        if (_trauma.HasWoundableTrauma(args.Part, ent.Comp.TraumaType))
            args.Cancelled = true;
    }

    private void OnBleedsTreatmentStep(Entity<SurgeryBleedsTreatmentStepComponent> ent, ref SurgeryStepEvent args)
    {
        var healAmount = ent.Comp.Amount;
        foreach (var woundEnt in _wounds.GetWoundableWoundsWithComp<BleedInflicterComponent>(args.Part))
        {
            var bleeds = woundEnt.Comp2;
            if (bleeds.BleedingAmountRaw > healAmount)
            {
                bleeds.BleedingAmountRaw -= healAmount;
            }
            else
            {
                bleeds.BleedingAmountRaw = 0;
                bleeds.SeverityPenalty = 0;
                bleeds.Scaling = 0;

                bleeds.IsBleeding = false; // Won't bleed as long as it's not reopened

                healAmount -= bleeds.BleedingAmountRaw;
            }

            DirtyField(woundEnt, bleeds, nameof(BleedInflicterComponent.IsBleeding));
        }
    }

    private void OnBleedsTreatmentCheck(Entity<SurgeryBleedsTreatmentStepComponent> ent, ref SurgeryStepCompleteCheckEvent args)
    {
        foreach (var woundEnt in _wounds.GetWoundableWounds(args.Part))
        {
            if (!TryComp<BleedInflicterComponent>(woundEnt, out var bleedsInflicter) || !bleedsInflicter.IsBleeding)
                continue;

            args.Cancelled = true;
            break;
        }
    }

    private void OnPainInflicterStep(Entity<SurgeryStepPainInflicterComponent> ent, ref SurgeryStepEvent args)
    {
        if (_mobState.IsDead(args.Body))
            return;

        if (!_consciousness.TryGetNerveSystem(args.Body, out var nerveSys))
            return;

        var painToInflict = ent.Comp.Amount;
        if (HasComp<ForcedSleepingStatusEffectComponent>(args.Body))
            painToInflict *= ent.Comp.SleepModifier;

        if (!_pain.TryChangePainModifier(
                nerveSys.Value.Owner,
                args.Part,
                "SurgeryPain",
                painToInflict,
                nerveSys,
                ent.Comp.PainDuration,
                ent.Comp.PainType))
        {
           _pain.TryAddPainModifier(nerveSys.Value.Owner,
               args.Part,
               "SurgeryPain",
               painToInflict,
               ent.Comp.PainType,
               nerveSys,
               ent.Comp.PainDuration);
        }
    }

    private void OnPainInflicterCheck(Entity<SurgeryStepPainInflicterComponent> ent, ref SurgeryStepCompleteCheckEvent args)
    {
        if (!_consciousness.TryGetNerveSystem(args.Part, out var nerveSys))
            return;

        if (!_pain.TryGetPainModifier(nerveSys.Value.Owner, args.Part, "SurgeryPain", out _, nerveSys))
            args.Cancelled = true;
    }

    private void OnSurgeryTargetStepChosen(Entity<SurgeryTargetComponent> ent, ref SurgeryStepChosenBuiMsg args)
    {
        var user = args.Actor;
        if (GetEntity(args.Entity) is not { Valid: true } body ||
            GetEntity(args.Part) is not { Valid: true } targetPart ||
            !CanReachSurgeryTarget(user, body) ||
            !IsSurgeryValid(body, targetPart, args.Surgery, args.Step, user, out var surgery, out var part, out var step))
        {
            return;
        }

        if (!PreviousStepsComplete(body, part, surgery, args.Step) ||
            IsStepComplete(body, part, args.Step, surgery))
            return;

        if (!CanPerformStep(user, body, part, step, true, out _, out _, out var validTools))
            return;

        if (HasComp<SurgeryAddPartStepComponent>(step)
            && !TryValidateAttachablePartInHands(user, body, surgery, out var attachPopup))
        {
            if (attachPopup != null)
                _popup.PopupEntity(attachPopup, user, user);
            return;
        }

        if (TryComp<SurgeryStepCavityEffectComponent>(step, out var cavityStep)
            && cavityStep.Action == "Insert"
            && !HasTinyOrSmallItemInHands(user))
        {
            _popup.PopupEntity(Loc.GetString("surgery-error-cavity-no-item"), user, user);
            return;
        }

        var speed = 1f;
        var usedEv = new SurgeryToolUsedEvent(user, body);
        // We need to check for nullability because of surgeries that dont require a tool, like Cavity Implants
        if (validTools?.Count > 0)
        {
            foreach (var (tool, toolSpeed) in validTools)
            {
                RaiseLocalEvent(tool, ref usedEv);
                if (usedEv.Cancelled)
                    return;

                speed *= toolSpeed;
            }

            if (_net.IsServer)
            {
                foreach (var tool in validTools.Keys)
                {
                    if (TryComp(tool, out SurgeryToolComponent? toolComp) &&
                        toolComp.EndSound != null)
                    {
                        _audio.PlayEntity(toolComp.StartSound, user, tool);
                    }
                }
            }
        }

        if (TryComp(body, out TransformComponent? xform))
            _rotateToFace.TryFaceCoordinates(user, _transform.GetMapCoordinates(body, xform).Position);

        var ev = new SurgeryDoAfterEvent(args.Surgery, args.Step);
        // TODO: Move 2 seconds to a field of SurgeryStepComponent
        var duration = 2f / speed;
        if (TryComp(user, out SurgerySpeedModifierComponent? surgerySpeedMod)
            && surgerySpeedMod is not null)
            duration = duration / surgerySpeedMod.SpeedModifier;

        var doAfter = new DoAfterArgs(EntityManager, user, TimeSpan.FromSeconds(duration), ev, body, part)
        {
            BreakOnMove = true,
            CancelDuplicate = true,
            DuplicateCondition = DuplicateConditions.SameEvent,
            NeedHand = true,
            BreakOnHandChange = true,
            // Surgery uses DoAfter.Args.Target == part, but parts/organs are often stored inside containers
            // and may not have reliable world coordinates (Nullspace-ish / 0,0). Range/access checks in
            // SharedDoAfterSystem.Update would cancel the do-after based on the part instead of the patient body.
            // We already validate reachability against the body before starting the step.
            DistanceThreshold = null,
            RequireCanInteract = false,
        };

        if (_doAfter.TryStartDoAfter(doAfter))
        {
            var userName = Identity.Entity(user, EntityManager);
            var targetName = Identity.Entity(ent.Owner, EntityManager);

            var locName = $"surgery-popup-procedure-{args.Surgery}-step-{args.Step}";
            var locResult = Loc.GetString(locName,
                ("user", userName), ("target", targetName), ("part", part));

            if (locResult == locName)
                locResult = Loc.GetString($"surgery-popup-step-{args.Step}",
                    ("user", userName), ("target", targetName), ("part", part));

            _popup.PopupEntity(locResult, user);
        }
    }

    private bool TryValidateAttachablePartInHands(EntityUid user, EntityUid body, Entity<SurgeryComponent> surgery, out string? popup)
    {
        popup = null;

        if (TryComp(surgery, out SurgeryOrganGraftAttachComponent? graftComp))
        {
            foreach (var tool in GetTools(user))
            {
                if (!TryComp<OrganComponent>(tool, out var organ) || organ.Body == body)
                    continue;

                if (graftComp.RequiredCategory is { } requiredCategory && organ.Category == requiredCategory)
                    return true;

                if (graftComp.SpiderLegSide is { } side
                    && organ.Category is { } legCategory
                    && IsSpiderLegGraftCategory(legCategory, side))
                    return true;
            }

            popup = Loc.GetString("surgery-error-missing-attachable-part");
            return false;
        }

        if (!TryComp<SurgeryPartRemovedConditionComponent>(surgery, out var removedComp))
            return true;

        foreach (var tool in GetTools(user))
        {
            if (TryComp<OrganComponent>(tool, out var organ)
                && organ.Body != body
                && _targeting.MatchesBodyPartType(tool, removedComp.Part, removedComp.Symmetry)
                && !(organ.Category is { } organCategory
                    && SurgeryBodyPartMapping.IsSpiderLegCategory(organCategory)))
                return true;

            if (TryComp<BodyPartComponent>(tool, out var partComp)
                && partComp.PartType == removedComp.Part
                && (removedComp.Symmetry == null || partComp.Symmetry == removedComp.Symmetry))
                return true;
        }

        popup = Loc.GetString("surgery-error-missing-attachable-part");
        return false;
    }

    private static bool IsSpiderLegGraftCategory(ProtoId<OrganCategoryPrototype> category, BodyPartSymmetry side)
    {
        if (!SurgeryBodyPartMapping.IsSpiderLegCategory(category))
            return false;

        var slots = side == BodyPartSymmetry.Left
            ? SurgeryBodyPartMapping.SpiderLegLeftSlots
            : SurgeryBodyPartMapping.SpiderLegRightSlots;

        return slots.Contains(category);
    }

    private bool HasTinyOrSmallItemInHands(EntityUid user)
    {
        foreach (var held in _hands.EnumerateHeld(user))
        {
            if (TryComp<ItemComponent>(held, out var item)
                && (item.Size.Id == "Tiny" || item.Size.Id == "Small"))
                return true;
        }

        return false;
    }

    private float GetSurgeryDuration(EntityUid surgeryStep, EntityUid user, EntityUid target, float toolSpeed)
    {
        if (!TryComp(surgeryStep, out SurgeryStepComponent? stepComp))
            return 2f; // Shouldnt really happen but just a failsafe.

        var speed = toolSpeed;

        if (TryComp(user, out SurgerySpeedModifierComponent? surgerySpeedMod))
            speed *= surgerySpeedMod.SpeedModifier;

        return stepComp.Duration / speed;
    }
    private (Entity<SurgeryComponent> Surgery, int Step)? GetNextStep(EntityUid body, EntityUid part, Entity<SurgeryComponent?> surgery, List<EntityUid> requirements)
    {
        if (!Resolve(surgery, ref surgery.Comp))
            return null;

        if (requirements.Contains(surgery))
            throw new ArgumentException($"Surgery {surgery} has a requirement loop: {string.Join(", ", requirements)}");

        requirements.Add(surgery);

        if (surgery.Comp.Requirement is { } requirementId &&
            GetSingleton(requirementId) is { } requirement &&
            GetNextStep(body, part, requirement, requirements) is { } requiredNext)
        {
            return requiredNext;
        }

        for (var i = 0; i < surgery.Comp.Steps.Count; i++)
        {
            var surgeryStep = surgery.Comp.Steps[i];
            if (!IsStepComplete(body, part, surgeryStep, surgery))
                return ((surgery, surgery.Comp), i);
        }

        return null;
    }

    public (Entity<SurgeryComponent> Surgery, int Step)? GetNextStep(EntityUid body, EntityUid part, EntityUid surgery)
    {
        return GetNextStep(body, part, surgery, new List<EntityUid>());
    }

    /// <summary>
    /// Returns the index of the next incomplete step within a specific surgery for UI highlighting.
    /// Unlike <see cref="GetNextStep"/>, does not return steps from requirement surgeries.
    /// </summary>
    public int? GetNextIncompleteStepIndex(EntityUid body, EntityUid part, EntityUid surgeryEnt)
    {
        if (!TryComp(surgeryEnt, out SurgeryComponent? surgeryComp))
            return null;

        if (surgeryComp.Requirement is { } requirementId
            && GetSingleton(requirementId) is { } requirementEnt
            && !IsSurgeryStepsComplete(body, part, requirementEnt))
            return null;

        for (var i = 0; i < surgeryComp.Steps.Count; i++)
        {
            if (!IsStepComplete(body, part, surgeryComp.Steps[i], surgeryEnt))
                return i;
        }

        return null;
    }

    public bool IsSurgeryStepsComplete(EntityUid body, EntityUid part, EntityUid surgeryEnt)
    {
        if (!TryComp(surgeryEnt, out SurgeryComponent? surgeryComp))
            return true;

        if (surgeryComp.Requirement is { } requirementId
            && GetSingleton(requirementId) is { } requirementEnt
            && !IsSurgeryStepsComplete(body, part, requirementEnt))
            return false;

        foreach (var step in surgeryComp.Steps)
        {
            if (!IsStepComplete(body, part, step, surgeryEnt))
                return false;
        }

        return true;
    }

    public bool PreviousStepsComplete(EntityUid body, EntityUid part, Entity<SurgeryComponent> surgery, EntProtoId step)
    {
        // TODO RMC14 use index instead of the prototype id
        if (surgery.Comp.Requirement is { } requirement)
        {
            if (GetSingleton(requirement) is not { } requiredEnt ||
                !TryComp(requiredEnt, out SurgeryComponent? requiredComp) ||
                !PreviousStepsComplete(body, part, (requiredEnt, requiredComp), step))
            {
                return false;
            }
        }

        foreach (var surgeryStep in surgery.Comp.Steps)
        {
            if (surgeryStep == step)
                break;

            if (!IsStepComplete(body, part, surgeryStep, surgery))
                return false;
        }

        return true;
    }

    public bool CanPerformStep(EntityUid user, EntityUid body, EntityUid part,
        EntityUid step, bool doPopup, out string? popup, out StepInvalidReason reason,
        out Dictionary<EntityUid, float>? validTools)
    {
        var type = _targeting.GetBodyPartType(part) ?? BodyPartType.Other;

        var slot = type switch
        {
            BodyPartType.Head => SlotFlags.HEAD,
            BodyPartType.Chest => SlotFlags.OUTERCLOTHING | SlotFlags.INNERCLOTHING,
            BodyPartType.Groin => SlotFlags.OUTERCLOTHING | SlotFlags.INNERCLOTHING,
            BodyPartType.Arm => SlotFlags.OUTERCLOTHING | SlotFlags.INNERCLOTHING,
            BodyPartType.Hand => SlotFlags.GLOVES,
            BodyPartType.Leg => SlotFlags.OUTERCLOTHING | SlotFlags.LEGS,
            BodyPartType.Foot => SlotFlags.FEET,
            BodyPartType.Tail => SlotFlags.NONE,
            BodyPartType.Other => SlotFlags.NONE,
            _ => SlotFlags.NONE
        };

        var check = new SurgeryCanPerformStepEvent(user, body, GetTools(user), slot);
        RaiseLocalEvent(step, ref check);
        popup = check.Popup;
        validTools = check.ValidTools;

        if (check.Invalid != StepInvalidReason.None)
        {
            if (doPopup && check.Popup != null)
                _popup.PopupEntity(check.Popup, user, user, PopupType.SmallCaution);

            reason = check.Invalid;
            return false;
        }

        reason = default;
        return true;
    }

    public bool CanPerformStep(EntityUid user, EntityUid body, EntityUid part, EntityUid step, bool doPopup)
    {
        return CanPerformStep(user, body, part, step, doPopup, out _, out _, out _);
    }

    public bool IsStepComplete(EntityUid body, EntityUid part, EntProtoId step, EntityUid surgery)
    {
        if (GetSingleton(step) is not { } stepEnt)
            return false;

        var ev = new SurgeryStepCompleteCheckEvent(body, part, surgery);
        RaiseLocalEvent(stepEnt, ref ev);
        return !ev.Cancelled;
    }

    private bool AnyHaveComp(List<EntityUid> tools, IComponent component, out EntityUid withComp, out float speed)
    {
        foreach (var tool in tools)
        {
            if (EntityManager.TryGetComponent(tool, component.GetType(), out var found) && found is ISurgeryToolComponent toolComp)
            {
                withComp = tool;
                speed = toolComp.Speed;
                return true;
            }
        }

        withComp = EntityUid.Invalid;
        speed = 1f;
        return false;
    }

    /// <summary>
    /// Re-applies profile and markings so reattached external organs match the mob appearance.
    /// </summary>
    private void RefreshBodyAppearance(EntityUid body, EntityUid? reattachedPart = null)
    {
        if (!TryComp<VisualBodyComponent>(body, out var visualBody))
            return;

        if (!_visualBody.TryGatherMarkingsData((body, visualBody), null, out var profiles, out _, out var applied))
            return;

        if (reattachedPart is { } partId
            && TryComp<OrganComponent>(partId, out var organ)
            && organ.Category is { } category
            && !profiles.ContainsKey(category)
            && profiles.TryGetValue("Torso", out var torsoProfile))
        {
            profiles[category] = torsoProfile;
        }

        _visualBody.ApplyProfiles(body, profiles);
        _visualBody.ApplyMarkings(body, applied);
    }
}
