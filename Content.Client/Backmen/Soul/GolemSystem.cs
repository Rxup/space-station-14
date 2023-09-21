using Content.Shared.Backmen.Soul;
using Robust.Shared.Containers;

namespace Content.Client.Backmen.Soul;

public sealed class GolemSystem : SharedGolemSystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GolemComponent, EntInsertedIntoContainerMessage>(OnEntInserted);
        SubscribeLocalEvent<GolemComponent, EntRemovedFromContainerMessage>(OnEntRemoved);
    }

    private void OnEntInserted(EntityUid uid, SharedGolemComponent component, EntInsertedIntoContainerMessage args)
    {
        SharedOnEntInserted(args);
    }

    private void OnEntRemoved(EntityUid uid, SharedGolemComponent component, EntRemovedFromContainerMessage args)
    {
        SharedOnEntRemoved(args);
    }
}
