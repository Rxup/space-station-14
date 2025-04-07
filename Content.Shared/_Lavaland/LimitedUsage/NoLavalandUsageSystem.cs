using Content.Shared._Lavaland.Procedural.Components;
using Content.Shared._Lavaland.Shuttles.Components;
using Content.Shared.Foldable;
using Content.Shared.Interaction;
using Content.Shared.Tools.Components;

namespace Content.Shared._Lavaland.LimitedUsage;

public sealed class NoLavalandUsageSystem : EntitySystem
{
    private EntityQuery<NoLavalandUsageComponent> _query;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NoLavalandUsageComponent, BoundUserInterfaceMessageAttempt>(OnBoundUserInterface,
            after: [typeof(SharedInteractionSystem)]);
        SubscribeLocalEvent<NoLavalandUsageComponent, FoldAttemptEvent>(OnOpenStorage);
        SubscribeLocalEvent<ToolUserAttemptUseEvent>(OnTryAnchor);
        _query = GetEntityQuery<NoLavalandUsageComponent>();
    }

    private void OnOpenStorage(Entity<NoLavalandUsageComponent> ent, ref FoldAttemptEvent args)
    {
        args.Cancelled = IsApply(ent);
    }

    private void OnTryAnchor(ref ToolUserAttemptUseEvent msg)
    {
        if (_query.HasComp(msg.Target))
        {
            msg.Cancelled = true;
        }
    }

    private void OnBoundUserInterface(Entity<NoLavalandUsageComponent> ent, ref BoundUserInterfaceMessageAttempt args)
    {
        if (IsApply(ent))
        {
            args.Cancel();
        }
    }

    public bool IsApply(EntityUid entity)
    {
        var xform = Transform(entity);

        if (HasComp<LavalandMapComponent>(xform.MapUid))
        {
            return true;
        }

        if (HasComp<MiningShuttleComponent>(xform.GridUid))
        {
            return true;
        }

        return false;
    }
}
