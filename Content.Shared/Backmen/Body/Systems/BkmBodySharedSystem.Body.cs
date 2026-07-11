using System.Linq;
using System.Numerics;
using Content.Shared.Backmen.Arachne;
using Content.Shared.Backmen.Body;
using Content.Shared.Backmen.Body.Components;
using Content.Shared.Backmen.Body.OrganRelations;
using Content.Shared.Backmen.Targeting;
using Content.Shared.Backmen.Surgery;
using Content.Shared.Backmen.Surgery.Traumas;
using Content.Shared.Backmen.Surgery.Traumas.Components;
using Content.Shared.Backmen.Surgery.Traumas.Systems;
using Content.Shared.Backmen.Surgery.Wounds.Components;
using Content.Shared.Backmen.Surgery.Wounds.Systems;
using Content.Shared.Body;
using Content.Shared.Body.Events;
using Content.Shared.Body.Part;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Gibbing.Components;
using Content.Shared.Gibbing.Events;
using Content.Shared.Gibbing.Systems;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Content.Shared.Inventory.VirtualItem;
using Content.Shared.Popups;
using Content.Shared.Rejuvenate;
using Content.Shared.Silicons.Borgs.Components;
using Content.Shared.Standing;
using JetBrains.Annotations;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared.Backmen.Body.Systems;

public partial class BkmBodySharedSystem
{
    /*
     * tl;dr of how bobby works
     * - BodyComponent uses a BodyPrototype as a template.
     * - On MapInit we spawn the root entity in the prototype and spawn all connections outwards from here
     * - Each "connection" is a body part (e.g. arm, hand, etc.) and each part can also contain organs.
     */

    [Dependency] private WoundSystem _woundSystem = default!; // backmen edit
    [Dependency] private ItemSlotsSystem _slots = default!;
    [Dependency] private LegacyGibbingSystem _gibbingSystem = default!;
    [Dependency] private SharedAudioSystem _audioSystem = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private TraumaSystem _trauma = default!; // backmen edit
    [Dependency] private OrganRelationInitializerSystem _organRelations = default!;
    private const float GibletLaunchImpulse = 8;
    private const float GibletLaunchImpulseVariance = 3;

    private void InitializeBody()
    {
        SubscribeLocalEvent<BodyComponent, OrganInsertedIntoEvent>(OnOrganInsertedIntoBody);
        SubscribeLocalEvent<BodyComponent, OrganRemovedFromEvent>(OnOrganRemovedFromBody);

        SubscribeLocalEvent<BodyComponent, StandAttemptEvent>(OnStandAttempt);
        SubscribeLocalEvent<BodyComponent, IsEquippingTargetAttemptEvent>(OnEquipTargetAttempt);

        // to prevent people from falling immediately as rejuvenated; backmen edit
        SubscribeLocalEvent<BodyComponent, RejuvenateEvent>(OnRejuvenate);
        SubscribeLocalEvent<BodyComponent, InitialBodySpawnedEvent>(OnInitialBodySpawned, after: [typeof(OrganRelationInitializerSystem)]);
    }

    /// <summary>
    /// Required leg count for movement and standing. Grafted arachne bodies use eight spider-leg slots.
    /// </summary>
    public int GetEffectiveRequiredLegs(EntityUid bodyId, BodyComponent? body = null)
    {
        if (!Resolve(bodyId, ref body, logMissing: false))
            return 0;

        if (BodyHasArachneOrgan(bodyId, body))
            return SurgeryBodyPartMapping.ArachneRequiredLegCount;

        return body.RequiredLegs;
    }

    /// <summary>
    /// Whether a leg organ category should be tracked in <see cref="BodyComponent.LegEntities"/>.
    /// Humanoids track biped legs; arachne-grafted bodies track spider legs only.
    /// </summary>
    public bool IsTrackedLegCategory(
        EntityUid bodyId,
        ProtoId<OrganCategoryPrototype> category,
        BodyComponent? body = null)
    {
        if (BodyHasArachneOrgan(bodyId, body))
            return SurgeryBodyPartMapping.IsSpiderLegCategory(category);

        return category == "LegLeft" || category == "LegRight";
    }

    private void OnArachneOrganBodyChanged(Entity<BodyComponent> ent)
    {
        SyncLegEntities(ent);
        UpdateMovementSpeed(ent, ent.Comp);
    }

    private void OnOrganInsertedIntoBody(Entity<BodyComponent> ent, ref OrganInsertedIntoEvent args)
    {
        if (!TryComp(args.Organ, out OrganComponent? organ))
            return;

        AddOrgan((args.Organ, organ), ent, ent);

        if (organ.Category is { } legCategory && IsTrackedLegCategory(ent, legCategory, ent.Comp))
        {
            ent.Comp.LegEntities.Add(args.Organ);
            UpdateMovementSpeed(ent);
            Dirty(ent, ent.Comp);
        }
        else if (HasComp<ArachneOrganComponent>(args.Organ))
        {
            OnArachneOrganBodyChanged(ent);
        }
    }

    private void OnOrganRemovedFromBody(Entity<BodyComponent> ent, ref OrganRemovedFromEvent args)
    {
        if (!TryComp(args.Organ, out OrganComponent? organ))
            return;

        if (organ.Category is { } removedCategory
            && SurgeryBodyPartMapping.TryGetDependentCategory(removedCategory, out var dependentCategory)
            && !TerminatingOrDeleted(ent)
            && !EntityManager.IsQueuedForDeletion(ent)
            && _nubody.TryGetOrganByCategory(ent.AsNullable(), dependentCategory, out var dependentOrgan)
            && !TerminatingOrDeleted(dependentOrgan)
            && !EntityManager.IsQueuedForDeletion(dependentOrgan))
        {
            // Surgery limb detachment moves the distal part with the proximal limb via DetachableOrganSystem.
            // Detached limb bundles keep the distal part until the proximal limb is reattached.
            var detachableSurgery = HasComp<DetachableOrganComponent>(args.Organ) && HasComp<SurgeryTargetComponent>(ent);
            var detachedLimbBundle = HasComp<BkmDetachedBodyComponent>(ent);

            if (!detachableSurgery
                && !detachedLimbBundle
                && Net.IsServer
                && ent.Comp.Organs != null
                && ent.Comp.Organs.Contains(dependentOrgan.Owner)
                && Containers.Remove(dependentOrgan.Owner, ent.Comp.Organs, force: true, reparent: false)
                && TryComp<OrganComponent>(dependentOrgan, out var dependentComp))
            {
                RemoveOrgan((dependentOrgan.Owner, dependentComp), ent.Owner);
                QueueDel(dependentOrgan.Owner);
            }
        }

        if (organ.Category is { } legCategory && IsTrackedLegCategory(ent, legCategory, ent.Comp))
        {
            ent.Comp.LegEntities.Remove(args.Organ);
            UpdateMovementSpeed(ent);
            if (!TerminatingOrDeleted(ent) && !EntityManager.IsQueuedForDeletion(ent))
                TryKnockdownWithoutEnoughLegs(ent);

            Dirty(ent, ent.Comp);
        }
        else if (organ.Category is { } category && SurgeryBodyPartMapping.IsHumanFootCategory(category))
        {
            TryDropFootwearWithoutHumanFeet(ent);
        }

        if (HasComp<ArachneOrganComponent>(args.Organ))
            OnArachneOrganBodyChanged(ent);

        RemoveOrgan((args.Organ, organ), ent);
    }

    private void OnStandAttempt(Entity<BodyComponent> ent, ref StandAttemptEvent args)
    {
        if (HasComp<BorgChassisComponent>(ent))
            return;

        if (ent.Comp.LegEntities.Count < GetEffectiveRequiredLegs(ent, ent.Comp))
            args.Cancel();
    }

    private void TryKnockdownWithoutEnoughLegs(Entity<BodyComponent> ent)
    {
        if (TerminatingOrDeleted(ent) || EntityManager.IsQueuedForDeletion(ent))
            return;

        if (ent.Comp.LegEntities.Count >= GetEffectiveRequiredLegs(ent, ent.Comp))
            return;

        if (TryComp<StandingStateComponent>(ent, out var standingState)
            && standingState.Standing
            && Standing.Down(ent, playSound: false, force: true, standingState: standingState))
        {
            var ev = new DropHandItemsEvent();
            RaiseLocalEvent(ent, ref ev);
        }
    }

    // backmen edit start
    [PublicAPI]
    public void ForceRestoreBody(Entity<BodyComponent?> ent, bool removeWounds = true)
    {
        if (!Resolve(ent, ref ent.Comp, logMissing: false))
            return;

        NubodyForceRestore(ent, removeWounds);
    }

    private void NubodyForceRestore(Entity<BodyComponent?> ent, bool removeWounds)
    {
        if (!Resolve(ent, ref ent.Comp) || ent.Comp!.Organs == null)
            return;

        if (TryComp<InitialBodyComponent>(ent, out var initialBody))
        {
            var hasArachneGraft = BodyHasArachneOrgan(ent, ent.Comp);

            foreach (var (category, proto) in initialBody.Organs)
            {
                if (hasArachneGraft && SurgeryBodyPartMapping.IsHumanLegOrFootCategory(category))
                    continue;

                if (_nubody.TryGetOrganByCategory((ent, ent.Comp), category, out var existing)
                    && !TerminatingOrDeleted(existing))
                    continue;

                var spawn = Spawn(proto);
                InsertOrganIntoBody(ent, spawn);
            }
        }

        foreach (var organ in GetBodyOrgans(ent, ent.Comp))
        {
            foreach (var modifier in organ.Component.IntegrityModifiers.ToList())
            {
                _trauma.TryRemoveOrganDamageModifier(organ.Id, modifier.Key.Item2, modifier.Key.Item1);
            }

            organ.Component.OrganIntegrity = organ.Component.IntegrityCap;
            organ.Component.OrganSeverity = OrganSeverity.Normal;
            Dirty(organ.Id, organ.Component);
        }

        if (!removeWounds)
            return;

        if (_trauma.TryGetBodyTraumas(ent, out var traumas, bodyComp: ent.Comp))
        {
            foreach (var trauma in traumas)
            {
                _trauma.RemoveTrauma(trauma);
            }
        }

        foreach (var woundable in GetWoundableTargets(ent, ent.Comp))
        {
            if (!TryComp<WoundableComponent>(woundable, out var woundableComp))
                continue;

            var bone = woundableComp.Bone.ContainedEntities.FirstOrNull();
            if (TryComp<BoneComponent>(bone, out var boneComp))
                _trauma.SetBoneIntegrity(bone.Value, boneComp.IntegrityCap, boneComp);

            _woundSystem.TryHaltAllBleeding(woundable, woundableComp);
            _woundSystem.ForceHealWoundsOnWoundable(woundable, out _);
        }

        _organRelations.WireRelationships((ent, ent.Comp!));
        SyncLegEntities((ent, ent.Comp!));
        UpdateMovementSpeed(ent, ent.Comp!);
    }

    private void OnRejuvenate(Entity<BodyComponent> ent, ref RejuvenateEvent args)
    {
        ForceRestoreBody(ent.AsNullable());
    }

    private void OnInitialBodySpawned(Entity<BodyComponent> ent, ref InitialBodySpawnedEvent args)
    {
        SyncLegEntities(ent);
        UpdateMovementSpeed(ent, ent.Comp);
    }

    private void SyncLegEntities(Entity<BodyComponent> bodyEnt)
    {
        bodyEnt.Comp.LegEntities.Clear();

        foreach (var (organUid, organ) in GetBodyOrgans(bodyEnt, bodyEnt.Comp))
        {
            if (organ.Category is { } category && IsTrackedLegCategory(bodyEnt, category, bodyEnt.Comp))
                bodyEnt.Comp.LegEntities.Add(organUid);
        }

        Dirty(bodyEnt, bodyEnt.Comp);
    }

    /// <summary>
    /// Rebuilds tracked leg organs after grafting or rejuvenation.
    /// </summary>
    public void SyncLegEntitiesForBody(Entity<BodyComponent> bodyEnt) => SyncLegEntities(bodyEnt);

    public IEnumerable<(EntityUid Id, OrganComponent Component)> GetBodyOrgans(
        EntityUid? bodyId,
        BodyComponent? body = null)
    {
        if (bodyId is null || !Resolve(bodyId.Value, ref body, logMissing: false))
            yield break;

        foreach (var organ in body!.Organs?.ContainedEntities.ToArray() ?? [])
        {
            if (TryComp(organ, out OrganComponent? organComp))
                yield return (organ, organComp);
        }
    }

    public virtual HashSet<EntityUid> GibBody(
        EntityUid bodyId,
        bool gibOrgans = false,
        BodyComponent? body = null,
        bool launchGibs = true,
        Vector2? splatDirection = null,
        float splatModifier = 1,
        Angle splatCone = default,
        SoundSpecifier? gibSoundOverride = null,
        GibType gib = GibType.Gib,
        GibContentsOption contents = GibContentsOption.Drop,
        List<string>? allowedContainers = null,
        List<string>? excludedContainers = null)
    {
        var gibs = new HashSet<EntityUid>();

        if (!Resolve(bodyId, ref body, logMissing: false))
            return gibs;

        var woundables = GetWoundableTargets(bodyId, body).ToList();
        woundables.Sort((a, b) => GetOrganRelationDepth(b).CompareTo(GetOrganRelationDepth(a)));

        var detachSys = EntityManager.System<DetachableOrganSystem>();
        using var violentDetach = detachSys.EnterViolentDetach(splatDirection, splatModifier);
        var bodyTransform = Transform(bodyId);

        foreach (var partUid in woundables)
        {
            if (TerminatingOrDeleted(partUid)
                || !TryComp<OrganComponent>(partUid, out var organ)
                || organ.Body != bodyId)
                continue;

            if (TryComp(partUid, out GibbableComponent? gibbable))
                gibSoundOverride ??= gibbable.GibSound;

            if (HasComp<DetachableOrganComponent>(partUid))
            {
                // Keep nested organs (e.g. brain in head) intact for DetachableOrganSystem to relocate.
                _gibbingSystem.TryGibEntityWithRef(
                    bodyId,
                    partUid,
                    GibType.Skip,
                    GibContentsOption.Skip,
                    ref gibs,
                    playAudio: false,
                    launchGibs: false,
                    allowedContainers: allowedContainers,
                    excludedContainers: excludedContainers);

                if (!RemoveOrgan(partUid, organ))
                    continue;

                if (TryGetDetachedBodyBundle(partUid, out var bundle))
                {
                    gibs.Add(bundle);
                }

                continue;
            }

            _gibbingSystem.TryGibEntityWithRef(bodyId, partUid, gib, contents, ref gibs, playAudio: false,
                launchGibs: true, launchDirection: splatDirection, launchImpulse: GibletLaunchImpulse * splatModifier,
                launchImpulseVariance: GibletLaunchImpulseVariance, launchCone: splatCone,
                allowedContainers: allowedContainers, excludedContainers: excludedContainers);
        }

        if (gibOrgans)
        {
            foreach (var organ in GetBodyOrgans(bodyId, body))
            {
                if (organ.Component.Category is { } category
                    && SurgeryBodyPartMapping.IsExternalCategory(category))
                    continue;

                // start-backmen: space-animal-organs
                if (TryComp<SpaceAnimalOrganComponent>(organ.Id, out var spaceOrgan)
                    && _random.NextFloat() > spaceOrgan.DropChance)
                    continue;
                // end-backmen: space-animal-organs

                _gibbingSystem.TryGibEntityWithRef(bodyId, organ.Id, GibType.Drop, GibContentsOption.Skip,
                    ref gibs, playAudio: false, launchImpulse: GibletLaunchImpulse * splatModifier,
                    launchImpulseVariance: GibletLaunchImpulseVariance, launchCone: splatCone,
                    allowedContainers: allowedContainers, excludedContainers: excludedContainers);

                // start-backmen: space-animal-organs
                if (TryComp<SpaceAnimalOrganComponent>(organ.Id, out spaceOrgan))
                {
                    var harvestEv = new OrganHarvestDamageEvent(spaceOrgan.HarvestDamageFraction);
                    RaiseLocalEvent(organ.Id, ref harvestEv);
                }
                // end-backmen: space-animal-organs
            }
        }

        if (TryComp<InventoryComponent>(bodyId, out var inventory))
        {
            foreach (var item in _inventorySystem.GetHandOrInventoryEntities(bodyId))
            {
                if (HasComp<VirtualItemComponent>(item))
                    continue;

                SharedTransform.DropNextTo(item, (bodyId, bodyTransform));
                gibs.Add(item);
            }
        }

        var scatter = EntityManager.System<BkmDetachedBodyScatterSystem>();
        foreach (var gibEntity in gibs)
        {
            if (HasComp<BkmDetachedBodyComponent>(gibEntity))
                scatter.ScatterViolentBundle(gibEntity, bodyTransform.Coordinates, splatDirection, splatModifier);
        }

        _audioSystem.PlayPredicted(gibSoundOverride, bodyTransform.Coordinates, null);
        return gibs;
    }

    private int GetOrganRelationDepth(EntityUid organId)
    {
        var depth = 0;
        var current = organId;

        while (TryComp<ChildOrganComponent>(current, out var child) && child.Parent is { } parent)
        {
            depth++;
            current = parent;
        }

        return depth;
    }

    private bool TryGetDetachedBodyBundle(EntityUid organId, out EntityUid bundle)
    {
        bundle = default;

        if (!Containers.TryGetContainingContainer(organId, out var container)
            || !HasComp<BkmDetachedBodyComponent>(container.Owner))
            return false;

        bundle = container.Owner;
        return true;
    }

    private void FlingGib(EntityUid target, Vector2? direction, float splatModifier, Angle scatterCone)
    {
        SharedTransform.AttachToGridOrMap(target);
        var scatterAngle = direction?.ToAngle() ?? _random.NextAngle();
        var scatterVector = _random.NextAngle(scatterAngle - scatterCone / 2, scatterAngle + scatterCone / 2)
            .ToVec() * (GibletLaunchImpulse * splatModifier + _random.NextFloat(GibletLaunchImpulseVariance));
        _physics.ApplyLinearImpulse(target, scatterVector);
    }

    private void TryDropFootwearWithoutHumanFeet(Entity<BodyComponent> ent)
    {
        if (HasBothHumanFeet(ent, ent.Comp) || !TryComp<InventoryComponent>(ent, out var inventory))
            return;

        _inventorySystem.DropSlotContents(ent, "shoes", inventory);
        _inventorySystem.DropSlotContents(ent, "socks", inventory);
    }

    private void OnEquipTargetAttempt(Entity<BodyComponent> ent, ref IsEquippingTargetAttemptEvent args)
    {
        if (!TryGetRequiredBodyPartForSlot(args.Slot, out var bodyPart))
            return;

        if (UsesFlatOrgans(args.EquipTarget))
            return;

        if (bodyPart == BodyPartType.Foot)
        {
            if (!HasBothHumanFeet(args.EquipTarget))
            {
                if (_timing.IsFirstTimePredicted)
                {
                    _popup.PopupEntity(Loc.GetString("equip-part-missing-error",
                        ("target", args.EquipTarget), ("part", bodyPart.ToString())), args.EquipTarget, args.EquipTarget);
                }

                args.Cancel();
            }

            return;
        }

        if (GetBodyPartCount(args.EquipTarget, bodyPart) == 0)
        {
            if (_timing.IsFirstTimePredicted)
                _popup.PopupEntity(Loc.GetString("equip-part-missing-error",
                    ("target", args.EquipTarget), ("part", bodyPart.ToString())), args.EquipTarget, args.EquipTarget);
            args.Cancel();
        }
    }

    // Shitmed Change End
}
