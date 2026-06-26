using System.Linq;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Backmen.Body.Systems;
using Content.Shared.Backmen.Body.OrganRelations;
using Content.Shared.Backmen.Body.Systems;
using Content.Shared.Body;
using Robust.Shared.Containers;

namespace Content.Server.Backmen.Body;

/// <summary>
/// Staged burn handling for detached body bundles on the ground.
/// </summary>
public sealed partial class BkmDetachedBodyBurnSystem : EntitySystem
{
    [Dependency] private BkmBodySharedSystem _body = default!;
    [Dependency] private BkmBrainPreservationSystem _brain = default!;
    [Dependency] private BkmBurnEffectsSystem _burnEffects = default!;
    [Dependency] private BkmDetachedBodyScatterSystem _scatter = default!;
    [Dependency] private FlammableSystem _flammable = default!;
    [Dependency] private OrganRelationSystem _organRelation = default!;
    [Dependency] private SharedContainerSystem _containers = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BkmDetachedBodyComponent, BurnDetachedBundleRootRequestEvent>(OnBurnDetachedBundleRootRequest);
    }

    private void OnBurnDetachedBundleRootRequest(Entity<BkmDetachedBodyComponent> ent, ref BurnDetachedBundleRootRequestEvent args)
    {
        if (ent.Comp.RootOrgan is { } root)
            BurnDetachedBundleRoot(ent, root);
    }

    /// <summary>
    /// Burns the bundle root to ash, ejects internals, and ignites them for cascade burning.
    /// </summary>
    public void BurnDetachedBundleRoot(Entity<BkmDetachedBodyComponent> bundle, EntityUid rootOrgan, EntityUid? cause = null)
    {
        if (!TryComp<BodyComponent>(bundle, out var body)
            || !_containers.TryGetContainer(bundle, BodyComponent.ContainerID, out var organContainer))
            return;

        foreach (var organUid in organContainer.ContainedEntities.ToArray())
        {
            if (TerminatingOrDeleted(organUid) || EntityManager.IsQueuedForDeletion(organUid))
                _containers.Remove(organUid, organContainer, force: true);
        }

        if (organContainer.Count == 0)
        {
            QueueDel(bundle);
            return;
        }

        var origin = Transform(bundle).Coordinates;
        var toEject = CollectBundleEjectSet(bundle.Comp, organContainer, rootOrgan);

        if (!TerminatingOrDeleted(rootOrgan) && organContainer.Contains(rootOrgan))
        {
            var rootCoords = Transform(rootOrgan).Coordinates;
            _burnEffects.SpawnAshAt(rootCoords);
            _burnEffects.PlayBurnSound(rootOrgan);
            _burnEffects.PopupPartBurn(rootOrgan, rootCoords);

            if (TryComp<OrganComponent>(rootOrgan, out var rootOrganComp))
                _body.RemoveOrgan(rootOrgan, rootOrganComp);

            if (organContainer.Contains(rootOrgan))
                _containers.Remove(rootOrgan, organContainer, force: true);

            QueueDel(rootOrgan);
            bundle.Comp.RootOrgan = null;
            Dirty(bundle, bundle.Comp);
        }

        foreach (var organUid in toEject)
        {
            if (!TryComp<OrganComponent>(organUid, out var organ))
                continue;

            if (_brain.TryPreserveBrain(organUid, origin, cause))
                continue;

            _body.RemoveOrgan(organUid, organ);

            if (organContainer.Contains(organUid))
                _containers.Remove(organUid, organContainer, force: true);

            _scatter.FlingViolentDetached(organUid, origin);
            _flammable.Ignite(organUid, cause ?? bundle);
        }

        foreach (var organUid in organContainer.ContainedEntities.ToArray())
            _containers.Remove(organUid, organContainer, force: true);

        if (!TerminatingOrDeleted(bundle) && !EntityManager.IsQueuedForDeletion(bundle))
            QueueDel(bundle);
    }

    private HashSet<EntityUid> CollectBundleEjectSet(
        BkmDetachedBodyComponent bundle,
        BaseContainer organContainer,
        EntityUid? root)
    {
        var toEject = new HashSet<EntityUid>();

        foreach (var organUid in organContainer.ContainedEntities)
        {
            if (TerminatingOrDeleted(organUid) || EntityManager.IsQueuedForDeletion(organUid))
                continue;

            if (root != null && organUid == root)
                continue;

            toEject.Add(organUid);
        }

        if (root is { } validRoot
            && !TerminatingOrDeleted(validRoot)
            && organContainer.Contains(validRoot))
        {
            foreach (var (organUid, _) in _body.GetOrgansForWoundable(validRoot))
                toEject.Add(organUid);

            foreach (var child in _organRelation.AllChildren(validRoot))
            {
                if (organContainer.Contains(child.Owner))
                    toEject.Add(child.Owner);
            }
        }

        return toEject;
    }
}
