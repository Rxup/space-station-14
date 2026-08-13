using System.Numerics;
using Content.Shared.Backmen.Teams;
using Content.Shared.Backmen.Teams.CapturePoint;
using Content.Shared.Backmen.Teams.CapturePoint.Components;
using Content.Shared.FixedPoint;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Utility;

namespace Content.Client.Backmen.Teams.CapturePoint;

public sealed partial class CapturePointSystem : SharedCapturePointSystem
{
    [Dependency] private SpriteSystem _sprite = default!;

    private static readonly SpriteSpecifier.Rsi BarSprite =
        new(new ResPath("/Textures/Interface/Misc/progress_bar.rsi"), "icon");

    private const float BarStartPx = 2f;
    private const float BarEndPx = 22f;
    private const float BarWidthPx = 24f;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BkmCapturePointComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<BkmCapturePointComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<BkmCapturePointComponent, AfterAutoHandleStateEvent>(OnAfterState);
    }

    private void OnStartup(Entity<BkmCapturePointComponent> ent, ref ComponentStartup args)
    {
        UpdateProgressBar(ent);
    }

    private void OnAfterState(Entity<BkmCapturePointComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        UpdateProgressBar(ent);
    }

    private void OnShutdown(Entity<BkmCapturePointComponent> ent, ref ComponentShutdown args)
    {
        if (!TryComp<SpriteComponent>(ent, out var sprite))
            return;

        RemoveLayer((ent.Owner, sprite), CaptureBarLayers.Background);
        RemoveLayer((ent.Owner, sprite), CaptureBarLayers.Fill);
    }

    private void UpdateProgressBar(Entity<BkmCapturePointComponent> ent)
    {
        if (!TryComp<SpriteComponent>(ent, out var sprite))
            return;

        var spriteEnt = (ent.Owner, sprite);
        EnsureLayers(spriteEnt);

        var ratio = (float) FixedPoint2.Min(1, ent.Comp.CaptureCurrent / ent.Comp.CaptureMax);
        var fillWidthPx = (BarEndPx - BarStartPx) * ratio;
        var yOffset = GetBarYOffset(spriteEnt);

        _sprite.LayerSetOffset(spriteEnt, CaptureBarLayers.Background, new Vector2(0f, yOffset));
        _sprite.LayerSetVisible(spriteEnt, CaptureBarLayers.Fill, fillWidthPx > 0f);
        if (fillWidthPx <= 0f)
            return;

        var fillCenterPx = -BarWidthPx / 2f + BarStartPx + fillWidthPx / 2f;
        _sprite.LayerSetScale(spriteEnt, CaptureBarLayers.Fill, new Vector2(fillWidthPx, 1f));
        _sprite.LayerSetOffset(spriteEnt,
            CaptureBarLayers.Fill,
            new Vector2(fillCenterPx / EyeManager.PixelsPerMeter, yOffset));
        _sprite.LayerSetColor(spriteEnt, CaptureBarLayers.Fill, GetProgressColor(ent.Comp.Team));
    }

    private void EnsureLayers(Entity<SpriteComponent?> spriteEnt)
    {
        if (_sprite.LayerMapTryGet(spriteEnt, CaptureBarLayers.Background, out _, false))
            return;

        var yOffset = _sprite.GetLocalBounds((spriteEnt.Owner, spriteEnt.Comp!)).Height / 2f + 0.05f;

        var bg = _sprite.AddLayer(spriteEnt, BarSprite);
        _sprite.LayerMapSet(spriteEnt, CaptureBarLayers.Background, bg);
        spriteEnt.Comp!.LayerSetShader(bg, "unshaded");
        _sprite.LayerSetOffset(spriteEnt, bg, new Vector2(0f, yOffset));

        var fill = _sprite.AddTextureLayer(spriteEnt, Texture.White);
        _sprite.LayerMapSet(spriteEnt, CaptureBarLayers.Fill, fill);
        spriteEnt.Comp.LayerSetShader(fill, "unshaded");
    }

    private float GetBarYOffset(Entity<SpriteComponent?> spriteEnt)
    {
        if (_sprite.TryGetLayer(spriteEnt, CaptureBarLayers.Background, out var layer, false))
            return layer.Offset.Y;

        return _sprite.GetLocalBounds((spriteEnt.Owner, spriteEnt.Comp!)).Height / 2f + 0.05f;
    }

    private void RemoveLayer(Entity<SpriteComponent?> spriteEnt, CaptureBarLayers key)
    {
        if (!_sprite.LayerMapTryGet(spriteEnt, key, out var layer, false))
            return;

        _sprite.RemoveLayer(spriteEnt, layer);
    }

    private static Color GetProgressColor(StationTeamMarker team)
    {
        return team switch
        {
            StationTeamMarker.TeamA => Color.DarkRed,
            StationTeamMarker.TeamB => Color.DarkBlue,
            _ => Color.DarkGray
        };
    }

    private enum CaptureBarLayers : byte
    {
        Background,
        Fill
    }
}
