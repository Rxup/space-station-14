using Content.Shared._Lavaland.Procedural.Components;
using Content.Shared._Lavaland.Shuttles.Components;
using Content.Shared.Backmen.Arrivals;
using Content.Shared.Construction.Components;
using Content.Shared.Foldable;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Prototypes;
using Content.Shared.Tools.Components;
using Content.Shared.UserInterface;
using Robust.Shared.Prototypes;

namespace Content.Shared._Lavaland.LimitedUsage;

public abstract partial class SharedNoLavalandUsageSystem : EntitySystem
{
    protected EntityQuery<NoLavalandUsageComponent> QueryLimit;

    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private IComponentFactory _componentFactory = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        QueryLimit = GetEntityQuery<NoLavalandUsageComponent>();

        SubscribeLocalEvent<BoundUserInterfaceMessageAttempt>(OnBoundUserInterface,
            after: [typeof(SharedInteractionSystem)]);
        SubscribeLocalEvent<NoLavalandUsageComponent, ActivatableUIOpenAttemptEvent>(OnTryOpenUi);
        SubscribeLocalEvent<NoLavalandUsageComponent, FoldAttemptEvent>(OnOpenStorage);
        SubscribeLocalEvent<NoLavalandUsageComponent, ToolUseAttemptEvent>(OnToolUseAttempt);
        SubscribeLocalEvent<NoLavalandUsageComponent, AnchorAttemptEvent>(OnAnchorAttempt);
        SubscribeLocalEvent<NoLavalandUsageComponent, UnanchorAttemptEvent>(OnUnanchorAttempt);
        SubscribeLocalEvent<Backmen.Arrivals.FlatPackUserAttemptUseEvent>(OnTryUnPack);
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

    private void OnToolUseAttempt(Entity<NoLavalandUsageComponent> ent, ref ToolUseAttemptEvent args)
    {
        if (args.Cancelled || !IsApply(ent))
            return;

        args.Cancel();
        _popup.PopupClient(Loc.GetString("nolavaland-usage-blocked"), ent, args.User);
    }

    private void OnAnchorAttempt(Entity<NoLavalandUsageComponent> ent, ref AnchorAttemptEvent args)
    {
        CancelAnchorIfBlocked(ent, args);
    }

    private void OnUnanchorAttempt(Entity<NoLavalandUsageComponent> ent, ref UnanchorAttemptEvent args)
    {
        CancelAnchorIfBlocked(ent, args);
    }

    private void CancelAnchorIfBlocked(Entity<NoLavalandUsageComponent> ent, BaseAnchoredAttemptEvent args)
    {
        if (args.Cancelled || !IsApply(ent))
            return;

        args.FailMessage = "nolavaland-usage-blocked";
        args.Cancel();
    }

    private void OnTryOpenUi(Entity<NoLavalandUsageComponent> ent, ref ActivatableUIOpenAttemptEvent args)
    {
        if (args.Cancelled || !IsApply(ent))
            return;

        if (!args.Silent)
            _popup.PopupClient(Loc.GetString("nolavaland-usage-blocked"), ent, args.User);

        args.Cancel();
    }

    private void OnBoundUserInterface(BoundUserInterfaceMessageAttempt args)
    {
        if (args.Cancelled)
            return;

        if (QueryLimit.HasComp(args.Target) && IsApply(args.Target))
            args.Cancel();
    }

    /// <summary>
    /// True when <paramref name="entity"/> has <see cref="NoLavalandUsageComponent"/>
    /// and is currently on Lavaland or a mining shuttle.
    /// </summary>
    public bool IsBlocked(EntityUid entity)
    {
        return QueryLimit.HasComp(entity) && IsApply(entity);
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
