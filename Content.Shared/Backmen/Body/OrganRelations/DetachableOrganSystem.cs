using Content.Shared.Backmen.Body.Systems;
using Content.Shared.Backmen.Surgery;
using Content.Shared.Backmen.Surgery.Wounds.Systems;
using Content.Shared.Backmen.Targeting;
using Content.Shared.Body;
using Content.Shared.Body.Organ;
using Robust.Shared.Containers;
using Robust.Shared.Network;

namespace Content.Shared.Backmen.Body.OrganRelations;

public sealed class DetachableOrganSystem : EntitySystem
{
    [Dependency] private EntityQuery<DetachableOrganComponent> _detachableOrgan = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private OrganRelationSystem _organRelation = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private WoundSystem _wounds = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DetachableOrganComponent, OrganGotRemovedEvent>(OnDetachableRemoved);
    }

    private void OnDetachableRemoved(Entity<DetachableOrganComponent> ent, ref OrganGotRemovedEvent args)
    {
        if (!_net.IsServer)
            return;

        if (TerminatingOrDeleted(ent) || TerminatingOrDeleted(args.Target))
            return;

        // Only detach limbs leaving a surgery patient — not when extracting from a limb bundle for reattachment.
        if (!HasComp<SurgeryTargetComponent>(args.Target))
            return;

        foreach (var parent in _organRelation.AllParents(ent.Owner))
        {
            if (_detachableOrgan.TryGetComponent(parent, out var detachableParent) && detachableParent.Detaching)
                return;
        }

        ent.Comp.Detaching = true;

        _organRelation.Orphan(ent.Owner);
        var body = PredictedSpawnNextToOrDrop(ent.Comp.DetachedBody, ent);

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
            if (!TryComp<OrganComponent>(child.Owner, out var childOrgan) || childOrgan.Body != args.Target)
                continue;

            if (!_container.Insert(child.Owner, container, force: true))
            {
                Log.Error($"{ToPrettyString(child)} could not be transferred to new body {ToPrettyString(body)}.");
                _organRelation.Orphan(child.AsNullable());
            }
        }

        ent.Comp.Detaching = false;

        _wounds.RefreshBodyTargetingStatus(args.Target);
    }
}
