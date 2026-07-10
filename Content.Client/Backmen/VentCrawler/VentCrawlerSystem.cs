using Content.Client.SubFloor;
using Content.Shared.Backmen.VentCrawler;
using Content.Shared.Eye;
using Content.Shared.SubFloor;
using Robust.Client.GameObjects;
using Robust.Client.Player;
using Robust.Shared.Timing;
using DrawDepth = Content.Shared.DrawDepth.DrawDepth;

namespace Content.Client.Backmen.VentCrawler;

public sealed partial class VentCrawlerSystem : SharedVentCrawlerSystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private SharedEyeSystem _eye = default!;
    [Dependency] private SharedVisibilitySystem _visibility = default!;
    [Dependency] private SpriteSystem _sprite = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private TrayScanRevealSystem _trayScanReveal = default!;
    [Dependency] private EntityQuery<SubFloorHideComponent> _subFloorHideQuery = default!;

    private const float RevealRange = 4f;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!_timing.IsFirstTimePredicted)
            return;

        var player = _player.LocalEntity;

        if (player is not { } playerUid || !HasComp<VentCrawlingComponent>(playerUid))
            return;

        if (!TryComp(playerUid, out TransformComponent? playerXform))
            return;

        var playerPos = _transform.GetWorldPosition(playerXform);
        var playerMap = playerXform.MapID;
        var inRange = new HashSet<Entity<SubFloorHideComponent>>();

        var entitiesInRange = new HashSet<Entity<SubFloorHideComponent>>();
        _lookup.GetEntitiesInRange(playerMap, playerPos, RevealRange, entitiesInRange, flags: TrayScannerSystem.Flags);

        foreach (var (uid, comp) in entitiesInRange)
        {
            inRange.Add((uid, comp));

            if (comp.IsUnderCover || _trayScanReveal.IsUnderRevealingEntity(uid))
                EnsureComp<TrayRevealedComponent>(uid);
        }

        var revealedQuery = AllEntityQuery<TrayRevealedComponent>();

        while (revealedQuery.MoveNext(out var uid, out _))
        {
            if (!_subFloorHideQuery.TryGetComponent(uid, out var subfloor) || !inRange.Contains((uid, subfloor)))
            {
                _appearance.SetData(uid, SubFloorVisuals.ScannerRevealed, false);
                RemCompDeferred<TrayRevealedComponent>(uid);
            }
            else
            {
                _appearance.SetData(uid, SubFloorVisuals.ScannerRevealed, true);
            }
        }
    }

    protected override void OnVentCrawlingStarted(Entity<VentCrawlingComponent> ent, ref ComponentStartup args)
    {
        if (TryComp<SpriteComponent>(ent, out var sprite))
        {
            ent.Comp.OriginalDrawDepth = sprite.DrawDepth;
            _sprite.SetDrawDepth((ent, sprite), (int) DrawDepth.BelowFloor);
        }

        ApplySubfloorVisibility(ent, ent.Comp);
        _eye.RefreshVisibilityMask(ent.Owner);
    }

    protected override void OnVentCrawlingStopped(Entity<VentCrawlingComponent> ent, ref ComponentShutdown args)
    {
        if (TerminatingOrDeleted(ent))
            return;

        if (TryComp<SpriteComponent>(ent, out var sprite) && ent.Comp.OriginalDrawDepth != null)
        {
            _sprite.SetDrawDepth((ent, sprite), ent.Comp.OriginalDrawDepth.Value);
            ent.Comp.OriginalDrawDepth = null;
        }

        RestoreVisibility(ent, ent.Comp);
        _eye.RefreshVisibilityMask(ent.Owner);
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
