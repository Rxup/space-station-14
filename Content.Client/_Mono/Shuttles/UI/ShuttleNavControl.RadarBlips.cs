// Copyright (c) 2024 Rane (https://github.com/shaeonee)
// Relicensed under AGPL-3.0-or-later with permission from Rane
using System.Numerics;
using Content.Client._Mono.Radar;
using Content.Shared._Lua.Shuttles.Components;
using Content.Shared._Mono.Radar;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Components;
using Robust.Shared.Timing;

namespace Content.Client.Shuttles.UI;

public partial class ShuttleNavControl
{
    // start-backmen: shuttle-gunnery
    private readonly RadarBlipsSystem _blips;
    private float _blipUpdateAccumulator;
    private const float BlipUpdateInterval = 0f;
    // end-backmen: shuttle-gunnery

    // start-backmen: shuttle-gunnery
    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        _blipUpdateAccumulator += args.DeltaSeconds;

        if (_blipUpdateAccumulator >= BlipUpdateInterval)
        {
            _blipUpdateAccumulator = 0;

            if (_consoleEntity != null)
                _blips.RequestBlips(_consoleEntity.Value);
        }
    }

    private void DrawMonoRadarBlips(
        DrawingHandleScreen handle,
        MapCoordinates mapPos,
        TransformComponent xform,
        Matrix3x2 worldToShuttle,
        Matrix3x2 shuttleToView)
    {
        var rawBlips = _blips.GetCurrentBlips();
        const int maxBlipsDraw = 320;
        var blipStride = Math.Max(1, rawBlips.Count / maxBlipsDraw);
        var viewBounds = new Box2(-3f, -3f, Size.X + 3f, Size.Y + 3f);

        for (var i = 0; i < rawBlips.Count; i += blipStride)
        {
            var blip = rawBlips[i];
            var blipMap = _transform.ToMapCoordinates(blip.Position);
            if (blipMap.MapId != xform.MapID)
                continue;

            if (Vector2.Distance(blipMap.Position, mapPos.Position) > CornerRadarRange)
                continue;

            var blipPosInView = Vector2.Transform(blipMap.Position, worldToShuttle * shuttleToView);

            if (!viewBounds.Contains(blipPosInView))
                continue;

            var handledByIcon = false;
            var blipEnt = EntManager.GetEntity(blip.NetUid);
            if (blipEnt != EntityUid.Invalid
                && EntManager.TryGetComponent<RadarBlipIconComponent>(blipEnt, out var blipIcon)
                && blipIcon.Icon != default)
            {
                var cache = IoCManager.Resolve<IResourceCache>();
                if (cache.TryGetResource<TextureResource>(blipIcon.Icon, out var texRes))
                {
                    var s = blip.Scale * 3.5f * blipIcon.Scale;
                    var half = new Vector2(s / 2f, s / 2f);
                    var box = new UIBox2(blipPosInView - half, blipPosInView + half);
                    handle.DrawTextureRect(texRes.Texture, box);
                    handledByIcon = true;
                }
            }

            if (!handledByIcon)
                DrawBlipShape(handle, blipPosInView, blip.Scale * 3f, blip.Color.WithAlpha(0.8f), blip.Shape);
        }

        var hitscanLines = _blips.GetHitscanLines();
        foreach (var line in hitscanLines)
        {
            var startPosInView = Vector2.Transform(line.Start, worldToShuttle * shuttleToView);
            var endPosInView = Vector2.Transform(line.End, worldToShuttle * shuttleToView);

            if (!viewBounds.Contains(startPosInView) && !viewBounds.Contains(endPosInView))
                continue;

            handle.DrawLine(startPosInView, endPosInView, line.Color);

            if (line.Thickness <= 1.0f)
                continue;

            var dir = (endPosInView - startPosInView).Normalized();
            var perpendicular = new Vector2(-dir.Y, dir.X) * 0.5f;

            for (float j = 1; j <= line.Thickness; j += 1.0f)
            {
                var offset = perpendicular * j;
                handle.DrawLine(startPosInView + offset, endPosInView + offset, line.Color);
                handle.DrawLine(startPosInView - offset, endPosInView - offset, line.Color);
            }
        }
    }

    private void DrawBlipShape(DrawingHandleScreen handle, Vector2 position, float size, Color color, RadarBlipShape shape)
    {
        switch (shape)
        {
            case RadarBlipShape.Circle:
                handle.DrawCircle(position, size, color);
                break;
            case RadarBlipShape.Square:
                var halfSize = size / 2;
                var rect = new UIBox2(
                    position.X - halfSize,
                    position.Y - halfSize,
                    position.X + halfSize,
                    position.Y + halfSize
                );
                handle.DrawRect(rect, color);
                break;
            case RadarBlipShape.Triangle:
                var points = new Vector2[]
                {
                    position + new Vector2(0, -size),
                    position + new Vector2(-size * 0.866f, size * 0.5f),
                    position + new Vector2(size * 0.866f, size * 0.5f)
                };
                handle.DrawPrimitives(DrawPrimitiveTopology.TriangleList, points, color);
                break;
            case RadarBlipShape.Star:
                DrawBlipStar(handle, position, size, color);
                break;
            case RadarBlipShape.Diamond:
                var diamondPoints = new Vector2[]
                {
                    position + new Vector2(0, -size),
                    position + new Vector2(size, 0),
                    position + new Vector2(0, size),
                    position + new Vector2(-size, 0)
                };
                handle.DrawPrimitives(DrawPrimitiveTopology.TriangleFan, diamondPoints, color);
                break;
            case RadarBlipShape.Hexagon:
                DrawBlipHexagon(handle, position, size, color);
                break;
            case RadarBlipShape.Arrow:
                DrawBlipArrow(handle, position, size, color);
                break;
        }
    }

    private static void DrawBlipStar(DrawingHandleScreen handle, Vector2 position, float size, Color color)
    {
        const int points = 5;
        const float innerRatio = 0.4f;
        var vertices = new Vector2[points * 2];

        for (var i = 0; i < points * 2; i++)
        {
            var angle = i * Math.PI / points;
            var radius = i % 2 == 0 ? size : size * innerRatio;
            vertices[i] = position + new Vector2(
                (float)Math.Sin(angle) * radius,
                -(float)Math.Cos(angle) * radius
            );
        }

        handle.DrawPrimitives(DrawPrimitiveTopology.TriangleFan, vertices, color);
    }

    private static void DrawBlipHexagon(DrawingHandleScreen handle, Vector2 position, float size, Color color)
    {
        var vertices = new Vector2[6];
        for (var i = 0; i < 6; i++)
        {
            var angle = i * Math.PI / 3;
            vertices[i] = position + new Vector2(
                (float)Math.Sin(angle) * size,
                -(float)Math.Cos(angle) * size
            );
        }

        handle.DrawPrimitives(DrawPrimitiveTopology.TriangleFan, vertices, color);
    }

    private static void DrawBlipArrow(DrawingHandleScreen handle, Vector2 position, float size, Color color)
    {
        var vertices = new Vector2[]
        {
            position + new Vector2(0, -size),
            position + new Vector2(-size * 0.5f, 0),
            position + new Vector2(0, size * 0.5f),
            position + new Vector2(size * 0.5f, 0)
        };

        handle.DrawPrimitives(DrawPrimitiveTopology.TriangleFan, vertices, color);
    }
    // end-backmen: shuttle-gunnery
}
