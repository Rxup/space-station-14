using System.Numerics;
using Content.Shared.Backmen.Surgery;
using Content.Shared.Backmen.Surgery.Wounds.Systems;
using Content.Shared.Backmen.Targeting;
using Content.Shared.Backmen.Body.Systems;
using Content.Shared.Body;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.Shared.Backmen.Body.OrganRelations;

public sealed partial class DetachableOrganSystem : EntitySystem
{
    private static readonly ProtoId<SoundCollectionPrototype> SurgeryAmputationSound = new("BkmSurgeryAmputation");

    [Dependency] private EntityQuery<DetachableOrganComponent> _detachableOrgan = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private BodySystem _body = default!;
    [Dependency] private OrganRelationSystem _organRelation = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private WoundSystem _wounds = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private BkmDetachedBodyScatterSystem _scatter = default!;

    private int _violentDetachDepth;
    private Vector2? _violentSplatDirection;
    private float _violentSplatModifier = 1f;

    public bool IsViolentDetach => _violentDetachDepth > 0;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DetachableOrganComponent, OrganGotRemovedEvent>(OnDetachableRemoved);
    }

    /// <summary>
    /// Marks nested organ removals during gib / violent destroy as wide-scatter detaches.
    /// </summary>
    public ViolentDetachScope EnterViolentDetach(Vector2? splatDirection = null, float splatModifier = 1f)
    {
        if (_violentDetachDepth == 0)
        {
            _violentSplatDirection = splatDirection;
            _violentSplatModifier = splatModifier;
        }

        return new ViolentDetachScope(this);
    }

    public sealed class ViolentDetachScope : IDisposable
    {
        private readonly DetachableOrganSystem _system;

        public ViolentDetachScope(DetachableOrganSystem system)
        {
            _system = system;
            _system._violentDetachDepth++;
        }

        public void Dispose()
        {
            _system._violentDetachDepth--;
            if (_system._violentDetachDepth == 0)
            {
                _system._violentSplatDirection = null;
                _system._violentSplatModifier = 1f;
            }
        }
    }

    private void OnDetachableRemoved(Entity<DetachableOrganComponent> ent, ref OrganGotRemovedEvent args)
    {
        if (!_net.IsServer)
            return;

        if (TerminatingOrDeleted(ent)
            || TerminatingOrDeleted(args.Target)
            || EntityManager.IsQueuedForDeletion(ent)
            || EntityManager.IsQueuedForDeletion(args.Target))
            return;

        // Only detach limbs leaving a surgery patient — not when extracting from a limb bundle for reattachment.
        if (!HasComp<SurgeryTargetComponent>(args.Target))
            return;

        foreach (var parent in _organRelation.AllParents(ent.Owner))
        {
            if (_detachableOrgan.TryGetComponent(parent, out var detachableParent) && detachableParent.Detaching)
                return;
        }

        var violent = IsViolentDetach;
        var context = violent ? BkmDetachContext.Violent : BkmDetachContext.Surgery;

        ent.Comp.Detaching = true;

        _organRelation.Orphan(ent.Owner);

        var spawnAt = violent ? args.Target : ent.Owner;
        var body = violent
            ? Spawn(ent.Comp.DetachedBody, Transform(args.Target).Coordinates)
            : PredictedSpawnNextToOrDrop(ent.Comp.DetachedBody, spawnAt);

        if (TryComp(body, out BkmDetachedBodyComponent? detachedBody))
        {
            detachedBody.MessyScatter = violent;
            Dirty(body, detachedBody);
        }

        if (!_container.TryGetContainer(body, BodyComponent.ContainerID, out var container))
        {
            Log.Error($"Entity {ToPrettyString(body)} relied on by {nameof(DetachableOrganComponent)} on {ToPrettyString(ent)} is missing a container ({BodyComponent.ContainerID}).");
            ent.Comp.Detaching = false;
            Del(body);
            return;
        }

        if (!_container.Insert(ent.Owner, container, force: true))
        {
            Log.Error($"{ToPrettyString(ent)} could not be transferred to new body {ToPrettyString(body)}.");
        }

        foreach (var child in _organRelation.AllChildren(ent.Owner))
        {
            if (TerminatingOrDeleted(child)
                || EntityManager.IsQueuedForDeletion(child)
                || !TryComp<OrganComponent>(child.Owner, out var childOrgan)
                || childOrgan.Body != args.Target)
                continue;

            if (!ShouldRelocateIntoDetachedBundle(ent.Owner, child.Owner))
                continue;

            if (!_container.Insert(child.Owner, container, force: true))
            {
                Log.Error($"{ToPrettyString(child)} could not be transferred to new body {ToPrettyString(body)}.");
                _organRelation.Orphan(child.AsNullable());
            }
        }

        ent.Comp.Detaching = false;

        RemoveStrandedDependentOrgan(args.Target, ent.Owner);

        if (violent
            && TryComp<OrganComponent>(ent.Owner, out var detachedOrgan)
            && detachedOrgan.Category == "Torso")
        {
            ScatterRemainingTorsoExternals(args.Target, ent.Owner);
        }

        if (violent)
        {
            _scatter.ScatterViolentBundle(
                body,
                Transform(args.Target).Coordinates,
                _violentSplatDirection,
                _violentSplatModifier);
        }
        else
        {
            _audio.PlayPvs(new SoundCollectionSpecifier(SurgeryAmputationSound), body);
        }

        var ev = new BkmDetachedBodyCreatedEvent(body, args.Target, context);
        RaiseLocalEvent(body, ref ev);

        _wounds.RefreshBodyTargetingStatus(args.Target);
    }

    /// <summary>
    /// If a proximal limb detached but its distal organ failed to move into the bundle, remove it from the patient
    /// so it can form its own detached bundle instead of staying orphaned on the body.
    /// </summary>
    private void RemoveStrandedDependentOrgan(EntityUid patient, EntityUid removedOrgan)
    {
        if (!TryComp<OrganComponent>(removedOrgan, out var removedOrganComp)
            || removedOrganComp.Category is not { } removedCategory
            || !SurgeryBodyPartMapping.TryGetDependentCategory(removedCategory, out var dependentCategory)
            || !_body.TryGetOrganByCategory(patient, dependentCategory, out var dependent)
            || !TryComp<OrganComponent>(dependent, out var dependentOrganComp)
            || dependentOrganComp.Body != patient)
            return;

        EntityManager.System<BkmBodySharedSystem>().RemoveOrgan(dependent, dependentOrganComp);
    }

    /// <summary>
    /// Torso organ relations include external limbs and head; those stay on the patient for separate bundles.
    /// </summary>
    private bool ShouldRelocateIntoDetachedBundle(EntityUid detachedOrgan, EntityUid childOrgan)
    {
        if (!TryComp<OrganComponent>(detachedOrgan, out var detachedComp) || detachedComp.Category is not { } detachedCategory)
            return true;

        if (!TryComp<OrganComponent>(childOrgan, out var childComp) || childComp.Category is not { } childCategory)
            return true;

        return detachedCategory != "Torso" || !SurgeryBodyPartMapping.IsExternalCategory(childCategory);
    }

    /// <summary>
    /// After a violent torso detach, peel remaining external limbs/head off into their own scattered bundles.
    /// </summary>
    private void ScatterRemainingTorsoExternals(EntityUid patient, EntityUid torso)
    {
        var externals = new List<EntityUid>();

        foreach (var child in _organRelation.AllChildren(torso))
        {
            if (!TryComp<OrganComponent>(child.Owner, out var organ) || organ.Body != patient)
                continue;

            if (organ.Category is not { } category || !SurgeryBodyPartMapping.IsExternalCategory(category))
                continue;

            externals.Add(child.Owner);
        }

        if (externals.Count == 0)
            return;

        externals.Sort((a, b) => GetOrganRelationDepth(b).CompareTo(GetOrganRelationDepth(a)));

        var bodySys = EntityManager.System<BkmBodySharedSystem>();
        foreach (var organUid in externals)
        {
            if (TerminatingOrDeleted(organUid)
                || !TryComp<OrganComponent>(organUid, out var organ)
                || organ.Body != patient)
                continue;

            bodySys.RemoveOrgan(organUid, organ);
        }
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
}

/// <summary>
/// Raised on a detached body bundle after organs are moved into it.
/// </summary>
[ByRefEvent]
public readonly record struct BkmDetachedBodyCreatedEvent(EntityUid Bundle, EntityUid SourceBody, BkmDetachContext Context);
