using Content.Shared.Actions;
using Content.Shared.Eye;
using Content.Shared.Hands;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.SubFloor;
using Content.Shared.Throwing;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Shared.Backmen.VentCrawler;

public abstract partial class SharedVentCrawlerSystem : EntitySystem
{
    public static readonly EntProtoId GasPipeBrokenPrototype = "GasPipeBroken";
    public static readonly EntProtoId ExitActionId = "ActionVentCrawlerExit";

    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedActionsSystem _actions = default!;

    private EntityQuery<BkmVentCrawlerVentComponent> _ventCrawlerVentQuery;
    private EntityQuery<SubFloorHideComponent> _subFloorHideQuery;

    public override void Initialize()
    {
        base.Initialize();

        _ventCrawlerVentQuery = GetEntityQuery<BkmVentCrawlerVentComponent>();
        _subFloorHideQuery = GetEntityQuery<SubFloorHideComponent>();

        // start-backmen: vent-crawler-interaction
        SubscribeLocalEvent<VentCrawlingComponent, AccessibleOverrideEvent>(OnAccessibleOverride);
        SubscribeLocalEvent<VentCrawlingComponent, InRangeOverrideEvent>(OnInRangeOverride);
        SubscribeLocalEvent<VentCrawlingComponent, InteractionAttemptEvent>(OnInteractionAttempt);
        SubscribeLocalEvent<VentCrawlingComponent, AttackAttemptEvent>(OnAttackAttempt);
        SubscribeLocalEvent<VentCrawlingComponent, GettingAttackedAttemptEvent>(OnGettingAttackedAttempt);
        SubscribeLocalEvent<VentCrawlingComponent, DropAttemptEvent>(OnDropAttempt);
        SubscribeLocalEvent<VentCrawlingComponent, ThrowAttemptEvent>(OnThrowAttempt);
        SubscribeLocalEvent<VentCrawlingComponent, ComponentStartup>(OnCrawlingStartup);
        SubscribeLocalEvent<VentCrawlingComponent, ComponentShutdown>(OnCrawlingShutdown);
        SubscribeLocalEvent<VentCrawlingComponent, GetVisMaskEvent>(OnGetVisMask);
        // end-backmen: vent-crawler-interaction
    }

    public bool IsVentCrawlerVent(EntityUid uid)
    {
        return _ventCrawlerVentQuery.HasComponent(uid);
    }

    // start-backmen: vent-crawler-interaction
    /// <summary>
    /// While crawling through vents, entities may only interact with their inventory, gas vents, and subfloor infrastructure.
    /// </summary>
    public bool CanVentCrawlerAccess(EntityUid user, EntityUid target)
    {
        if (user == target)
            return true;

        // Allow inventory, hands, and nested storage — but not other loose world entities on the floor.
        if (_container.IsInSameOrParentContainer(user, target, out var userContainer, out var targetContainer) &&
            (userContainer != null || targetContainer != null))
            return true;

        if (IsVentCrawlerVent(target))
            return true;

        if (_subFloorHideQuery.HasComponent(target))
            return true;

        return false;
    }

    private void OnAccessibleOverride(Entity<VentCrawlingComponent> ent, ref AccessibleOverrideEvent args)
    {
        if (args.Handled || args.Accessible || args.User != ent.Owner)
            return;

        if (!CanVentCrawlerAccess(args.User, args.Target))
        {
            args.Handled = true;
            args.Accessible = false;
            return;
        }

        if (IsVentCrawlerVent(args.Target) || _subFloorHideQuery.HasComponent(args.Target))
        {
            args.Handled = true;
            args.Accessible = true;
        }
    }

    private void OnInRangeOverride(Entity<VentCrawlingComponent> ent, ref InRangeOverrideEvent args)
    {
        if (args.Handled || args.User != ent.Owner)
            return;

        if (CanVentCrawlerAccess(args.User, args.Target))
            return;

        args.Handled = true;
        args.InRange = false;
    }

    private void OnInteractionAttempt(Entity<VentCrawlingComponent> ent, ref InteractionAttemptEvent args)
    {
        if (args.Uid != ent.Owner || args.Target == null)
            return;

        if (CanVentCrawlerAccess(args.Uid, args.Target.Value))
            return;

        args.Cancelled = true;
    }

    private void OnAttackAttempt(EntityUid uid, VentCrawlingComponent component, AttackAttemptEvent args)
    {
        args.Cancel();
    }

    private void OnGettingAttackedAttempt(EntityUid uid, VentCrawlingComponent component, ref GettingAttackedAttemptEvent args)
    {
        args.Cancelled = true;
    }

    private void OnDropAttempt(EntityUid uid, VentCrawlingComponent component, DropAttemptEvent args)
    {
        args.Cancel();
    }

    private void OnThrowAttempt(EntityUid uid, VentCrawlingComponent component, ThrowAttemptEvent args)
    {
        args.Cancel();
    }

    private void OnCrawlingStartup(Entity<VentCrawlingComponent> ent, ref ComponentStartup args)
    {
        _actions.AddAction(ent, ref ent.Comp.ExitActionEntity, ExitActionId, ent);
        Dirty(ent, ent.Comp);
        OnVentCrawlingStarted(ent, ref args);
    }

    private void OnCrawlingShutdown(Entity<VentCrawlingComponent> ent, ref ComponentShutdown args)
    {
        OnVentCrawlingStopped(ent, ref args);

        _actions.RemoveAction(ent.Owner, ent.Comp.ExitActionEntity);
        ent.Comp.ExitActionEntity = null;
    }

    protected virtual void OnVentCrawlingStarted(Entity<VentCrawlingComponent> ent, ref ComponentStartup args)
    {
    }

    protected virtual void OnVentCrawlingStopped(Entity<VentCrawlingComponent> ent, ref ComponentShutdown args)
    {
    }

    private void OnGetVisMask(EntityUid uid, VentCrawlingComponent component, ref GetVisMaskEvent args)
    {
        args.VisibilityMask |= (int) VisibilityFlags.Subfloor;
    }
    // end-backmen: vent-crawler-interaction
}
