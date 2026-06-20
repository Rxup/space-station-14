using Content.Shared.Backmen.Body.OrganRelations;
using Robust.Shared.Containers;

namespace Content.Server.Backmen.Body;

public sealed class BkmDetachedBodySystem : EntitySystem
{
    [Dependency] private Shared.Backmen.Body.OrganRelations.BkmDetachedBodySystem _detached = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BkmDetachedBodyComponent, EntInsertedIntoContainerMessage>(_detached.OnOrganInserted);
        SubscribeLocalEvent<BkmDetachedBodyComponent, EntRemovedFromContainerMessage>(_detached.OnOrganRemoved);
    }
}
