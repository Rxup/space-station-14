using Content.Shared.Backmen.VentCrawler;
using Content.Shared.Eye;
using Robust.Client.GameObjects;
using DrawDepth = Content.Shared.DrawDepth.DrawDepth;

namespace Content.Client.Backmen.VentCrawler;

public sealed class VentCrawlerSystem : SharedVentCrawlerSystem
{
    [Dependency] private readonly SharedVisibilitySystem _visibility = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<VentCrawlingComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<VentCrawlingComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<VentCrawlingComponent, GetVisMaskEvent>(OnGetVisMask);
    }

    private void OnStartup(EntityUid uid, VentCrawlingComponent component, ComponentStartup args)
    {
        if (TryComp<SpriteComponent>(uid, out var sprite))
        {
            component.OriginalDrawDepth = sprite.DrawDepth;
            _sprite.SetDrawDepth((uid, sprite), (int) DrawDepth.BelowFloor);
        }

        ApplySubfloorVisibility(uid, component);
    }

    private void OnShutdown(EntityUid uid, VentCrawlingComponent component, ComponentShutdown args)
    {
        if (TerminatingOrDeleted(uid))
            return;

        if (TryComp<SpriteComponent>(uid, out var sprite) && component.OriginalDrawDepth != null)
        {
            _sprite.SetDrawDepth((uid, sprite), component.OriginalDrawDepth.Value);
            component.OriginalDrawDepth = null;
        }

        RestoreVisibility(uid, component);
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

    private void RestoreVisibility(EntityUid uid, VentCrawlingComponent component)
    {
        if (!component.HadVisibility || !TryComp<VisibilityComponent>(uid, out var visibility))
            return;

        _visibility.RemoveLayer((uid, visibility), (int) VisibilityFlags.Subfloor, false);
        _visibility.AddLayer((uid, visibility), (int) VisibilityFlags.Normal, false);
        _visibility.RefreshVisibility(uid);
        component.HadVisibility = false;
    }
}
