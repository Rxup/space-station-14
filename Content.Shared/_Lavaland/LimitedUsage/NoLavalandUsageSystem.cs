using Content.Shared._Lavaland.Procedural.Components;
using Content.Shared._Lavaland.Shuttles.Components;
using Content.Shared.Backmen.Arrivals;
using Content.Shared.Foldable;
using Content.Shared.Interaction;
using Content.Shared.Prototypes;
using Content.Shared.Tools.Components;
using Robust.Shared.Prototypes;

namespace Content.Shared._Lavaland.LimitedUsage;

public abstract class SharedNoLavalandUsageSystem : EntitySystem
{
    protected EntityQuery<NoLavalandUsageComponent> QueryLimit;

    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly IComponentFactory _componentFactory = default!;


    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NoLavalandUsageComponent, BoundUserInterfaceMessageAttempt>(OnBoundUserInterface,
            after: [typeof(SharedInteractionSystem)]);
        SubscribeLocalEvent<NoLavalandUsageComponent, FoldAttemptEvent>(OnOpenStorage);
        SubscribeLocalEvent<ToolUserAttemptUseEvent>(OnTryAnchor);
        SubscribeLocalEvent<Backmen.Arrivals.FlatPackUserAttemptUseEvent>(OnTryUnPack);

        QueryLimit = GetEntityQuery<NoLavalandUsageComponent>();
    }

    private void OnTryUnPack(ref FlatPackUserAttemptUseEvent ev)
    {
        if (IsApply(ev.User) && _prototypes.TryIndex(ev.ItemToSpawn, out var prot) && prot.HasComponent<NoLavalandUsageComponent>(_componentFactory))
        {
            ev.Cancelled = true;
        }
    }

    private void OnOpenStorage(Entity<NoLavalandUsageComponent> ent, ref FoldAttemptEvent args)
    {
        args.Cancelled = IsApply(ent);
    }

    private void OnTryAnchor(ref ToolUserAttemptUseEvent msg)
    {
        if (QueryLimit.HasComp(msg.Target) && IsApply(msg.Target.Value))
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
