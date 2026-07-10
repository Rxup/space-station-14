using Content.Shared.Actions;
using Content.Shared.Eye;
using Content.Shared.Hands;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Pulling.Events;
using Content.Shared.SubFloor;
using Content.Shared.Throwing;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Shared.Backmen.VentCrawler;

public abstract partial class SharedVentCrawlerSystem : EntitySystem
{
    public static readonly EntProtoId GasPipeBrokenPrototype = "GasPipeBroken";
    public static readonly EntProtoId ExitActionId = "ActionVentCrawlerExit";

    /// <summary>
    /// Built-in subfloor reveal range while crawling (matches default t-ray range).
    /// </summary>
    public const float VentCrawlerRevealRange = 4f;

    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private SharedEyeSystem _eye = default!;
    [Dependency] private SharedVisibilitySystem _visibility = default!;

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
        SubscribeLocalEvent<VentCrawlingComponent, GettingInteractedWithAttemptEvent>(OnGettingInteractedWithAttempt);
        SubscribeLocalEvent<VentCrawlingComponent, BeingPulledAttemptEvent>(OnBeingPulledAttempt);
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

        if (IsUserOwnedItem(user, target))
            return true;

        if (IsVentCrawlerVent(target))
            return true;

        if (_subFloorHideQuery.HasComponent(target))
            return true;

        return false;
    }

    private bool IsUserOwnedItem(EntityUid user, EntityUid target)
    {
        if (_container.IsInSameOrParentContainer(user, target, out _, out var targetContainer) &&
            targetContainer != null)
            return true;

        if (!_container.TryGetContainingContainer(target, out var container))
            return false;

        if (container.Owner == user)
            return true;

        return TryComp(container.Owner, out TransformComponent? ownerXform) && ownerXform.ParentUid == user;
    }

    private void OnAccessibleOverride(Entity<VentCrawlingComponent> ent, ref AccessibleOverrideEvent args)
    {
        if (args.Target == ent.Owner && args.User != ent.Owner)
        {
            args.Handled = true;
            args.Accessible = false;
            return;
        }

        if (args.Handled || args.Accessible || args.User != ent.Owner)
            return;

        if (CanVentCrawlerAccess(args.User, args.Target))
        {
            args.Handled = true;
            args.Accessible = true;
            return;
        }

        args.Handled = true;
        args.Accessible = false;
    }

    private void OnInRangeOverride(Entity<VentCrawlingComponent> ent, ref InRangeOverrideEvent args)
    {
        if (args.Target == ent.Owner && args.User != ent.Owner)
        {
            args.Handled = true;
            args.InRange = false;
            return;
        }

        if (args.Handled || args.User != ent.Owner)
            return;

        if (CanVentCrawlerAccess(args.User, args.Target))
        {
            args.Handled = true;
            args.InRange = true;
            return;
        }

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

    private void OnGettingInteractedWithAttempt(EntityUid uid, VentCrawlingComponent component, ref GettingInteractedWithAttemptEvent args)
    {
        if (args.Uid == uid)
            return;

        args.Cancelled = true;
    }

    private void OnBeingPulledAttempt(EntityUid uid, VentCrawlingComponent component, BeingPulledAttemptEvent args)
    {
        args.Cancel();
    }

    private void OnCrawlingStartup(Entity<VentCrawlingComponent> ent, ref ComponentStartup args)
    {
        ApplySubfloorVisibility(ent, ent.Comp);
        _eye.RefreshVisibilityMask(ent.Owner);

        _actions.AddAction(ent, ref ent.Comp.ExitActionEntity, ExitActionId, ent);
        Dirty(ent, ent.Comp);
        OnVentCrawlingStarted(ent, ref args);
    }

    private void OnCrawlingShutdown(Entity<VentCrawlingComponent> ent, ref ComponentShutdown args)
    {
        OnVentCrawlingStopped(ent, ref args);

        RestoreSubfloorVisibility(ent, ent.Comp);
        _eye.RefreshVisibilityMask(ent.Owner);

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

    private void ApplySubfloorVisibility(EntityUid uid, VentCrawlingComponent component)
    {
        var visibility = EnsureComp<VisibilityComponent>(uid);
        component.HadVisibility = true;
        _visibility.RemoveLayer((uid, visibility), (int) VisibilityFlags.Normal, false);
        _visibility.AddLayer((uid, visibility), (int) VisibilityFlags.Subfloor, false);
        _visibility.RefreshVisibility(uid);
    }

    private void RestoreSubfloorVisibility(EntityUid uid, VentCrawlingComponent component)
    {
        if (!component.HadVisibility || !TryComp<VisibilityComponent>(uid, out var visibility))
            return;

        _visibility.RemoveLayer((uid, visibility), (int) VisibilityFlags.Subfloor, false);
        _visibility.AddLayer((uid, visibility), (int) VisibilityFlags.Normal, false);
        _visibility.RefreshVisibility(uid);
        component.HadVisibility = false;
    }
    // end-backmen: vent-crawler-interaction
}
