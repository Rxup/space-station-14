using Content.Server._Impstation.Revenant.EntitySystems;
using Content.Server.Xenoarchaeology.Artifact.XAE.Components;
using Content.Shared.Item;
using Content.Shared.Xenoarchaeology.Artifact;
using Content.Shared.Xenoarchaeology.Artifact.XAE;

namespace Content.Server.Xenoarchaeology.Artifact.XAE;

public sealed partial class XAEAnimateNearbySystem : BaseXAESystem<XAEAnimateNearbyComponent>
{
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private RevenantAnimatedSystem _animated = default!;

    private readonly HashSet<EntityUid> _entities = new();

    protected override void OnActivated(Entity<XAEAnimateNearbyComponent> ent, ref XenoArtifactNodeActivatedEvent args)
    {
        _entities.Clear();
        _lookup.GetEntitiesInRange(ent, ent.Comp.Range, _entities, LookupFlags.Dynamic | LookupFlags.Sundries);

        foreach (var entity in _entities)
        {
            if (!HasComp<ItemComponent>(entity))
                continue;

            _animated.TryAnimateObject(entity, ent.Comp.Duration);
        }
    }
}
