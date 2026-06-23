using Content.Shared.Backmen.Body.OrganRelations;
using Content.Shared.Body;
using Robust.Client.GameObjects;
using Robust.Shared.Containers;

namespace Content.Client.Backmen.Body;

/// <summary>
/// Ensures detached limb bundles show organ layers after organs are moved into the runtime shell.
/// </summary>
public sealed class BkmDetachedBodyVisualSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BkmDetachedBodyComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<BkmDetachedBodyComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<BkmDetachedBodyComponent, EntInsertedIntoContainerMessage>(OnContainerInserted);
    }

    private void OnStartup(Entity<BkmDetachedBodyComponent> ent, ref ComponentStartup args) =>
        RefreshVisuals(ent);

    private void OnMapInit(Entity<BkmDetachedBodyComponent> ent, ref MapInitEvent args) =>
        RefreshVisuals(ent);

    private void OnContainerInserted(Entity<BkmDetachedBodyComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        if (args.Container.ID != BodyComponent.ContainerID)
            return;

        RefreshVisuals(ent);
    }

    private void RefreshVisuals(Entity<BkmDetachedBodyComponent> ent)
    {
        if (!TryComp(ent, out BodyComponent? body) || body.Organs == null)
            return;

        foreach (var organ in body.Organs.ContainedEntities)
        {
            var inserted = new OrganGotInsertedEvent(ent);
            RaiseLocalEvent(organ, ref inserted);
        }
    }
}
