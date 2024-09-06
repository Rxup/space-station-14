using Content.Shared.Body.Systems;
using Content.Shared.Buckle.Components;
using Content.Shared.Crawling;
using Content.Shared.Movement.Events;
using Content.Shared.Movement.Systems;
using Content.Shared.Standing;
using Content.Shared.Throwing;

namespace Content.Shared.Traits.Assorted;

public sealed class LegsParalyzedSystem : EntitySystem
{
    [Dependency] private readonly CrawlingSystem _crawlingSystem = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _movementSpeedModifierSystem = default!;
    [Dependency] private readonly StandingStateSystem _standingSystem = default!;
    [Dependency] private readonly SharedBodySystem _bodySystem = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<LegsParalyzedComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<LegsParalyzedComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<LegsParalyzedComponent, BuckledEvent>(OnBuckled);
        SubscribeLocalEvent<LegsParalyzedComponent, UnbuckledEvent>(OnUnbuckled);
        SubscribeLocalEvent<LegsParalyzedComponent, ThrowPushbackAttemptEvent>(OnThrowPushbackAttempt);
        SubscribeLocalEvent<LegsParalyzedComponent, UpdateCanMoveEvent>(OnUpdateCanMoveEvent);
        SubscribeLocalEvent<LegsParalyzedComponent, CrawlStandupDoAfterEvent>(OnCrawlStandup);
        SubscribeLocalEvent<LegsParalyzedComponent, CrawlingKeybindEvent>(OnCrawlKeybind);
    }

    private void OnStartup(EntityUid uid, LegsParalyzedComponent component, ComponentStartup args)
    {
        if (!TryComp<CrawlerComponent>(uid, out var crawlerComp))
        {
            _movementSpeedModifierSystem.ChangeBaseSpeed(uid, 0, 0, 20);
            return;
        }

        if (!HasComp<BuckleComponent>(uid))
            _crawlingSystem.SetCrawling(uid, crawlerComp, true);
    }

    private void OnShutdown(EntityUid uid, LegsParalyzedComponent component, ComponentShutdown args)
    {
        _standingSystem.Stand(uid);
        _bodySystem.UpdateMovementSpeed(uid);
    }

    private void OnBuckled(EntityUid uid, LegsParalyzedComponent component, ref BuckledEvent args)
    {
        if (!HasComp<CrawlerComponent>(uid))
            _standingSystem.Stand(uid);
    }

    private void OnUnbuckled(EntityUid uid, LegsParalyzedComponent component, ref UnbuckledEvent args)
    {
        if (TryComp<CrawlerComponent>(uid, out var crawlerComp))
            _crawlingSystem.SetCrawling(uid, crawlerComp, true);
    }

    private void OnUpdateCanMoveEvent(EntityUid uid, LegsParalyzedComponent component, UpdateCanMoveEvent args)
    {
        if (!HasComp<CrawlerComponent>(uid))
            args.Cancel();
    }

    private void OnThrowPushbackAttempt(EntityUid uid, LegsParalyzedComponent component, ThrowPushbackAttemptEvent args)
    {
        if (!HasComp<CrawlerComponent>(uid))
            args.Cancel();
    }

    private void OnCrawlStandup(EntityUid uid, LegsParalyzedComponent component, CrawlStandupDoAfterEvent args)
    {
        args.Handled = true;
    }

    private void OnCrawlKeybind(EntityUid uid, LegsParalyzedComponent component, CrawlingKeybindEvent args)
    {
        args.Cancelled = true;
    }
    }
}
