using System.Numerics;
using Content.Client.UserInterface.Systems;
using Content.Shared.Backmen.Teams;
using Content.Shared.Backmen.Teams.CapturePoint.Components;
using Content.Shared.FixedPoint;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Containers;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client.Backmen.Teams.CapturePoint;

public sealed class CapturePointOverlay : Overlay
{
    private readonly IEntityManager _entManager;
    private readonly IGameTiming _timing;
    private readonly IPlayerManager _player;
    private readonly SharedTransformSystem _transform;

    private readonly Texture _barTexture;
    private readonly ShaderInstance _unshadedShader;

    public CapturePointOverlay(IEntityManager entManager, IPrototypeManager protoManager, IGameTiming timing, IPlayerManager player)
    {
        _entManager = entManager;
        _timing = timing;
        _player = player;
        _transform = _entManager.EntitySysManager.GetEntitySystem<SharedTransformSystem>();
        var sprite = new SpriteSpecifier.Rsi(new("/Textures/Interface/Misc/progress_bar.rsi"), "icon");
        _barTexture = _entManager.EntitySysManager.GetEntitySystem<SpriteSystem>().Frame0(sprite);

        _unshadedShader = protoManager.Index<ShaderPrototype>("unshaded").Instance();
    }

    /// <summary>
    ///     Flash time for cancelled DoAfters
    /// </summary>
    private const float FlashTime = 0.125f;

    // Hardcoded width of the progress bar because it doesn't match the texture.
    private const float StartX = 2;
    private const float EndX = 22f;

    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowFOV;

    protected override void Draw(in OverlayDrawArgs args)
    {
        var handle = args.WorldHandle;
        var rotation = args.Viewport.Eye?.Rotation ?? Angle.Zero;
        var xformQuery = _entManager.GetEntityQuery<TransformComponent>();

        // If you use the display UI scale then need to set max(1f, displayscale) because 0 is valid.
        const float scale = 1f;
        var scaleMatrix = Matrix3Helpers.CreateScale(new Vector2(scale, scale));
        var rotationMatrix = Matrix3Helpers.CreateRotation(-rotation);

        var curTime = _timing.CurTime;

        var bounds = args.WorldAABB.Enlarged(5f);
        var localEnt = _player.LocalSession?.AttachedEntity;

        var metaQuery = _entManager.GetEntityQuery<MetaDataComponent>();
        var enumerator = _entManager.AllEntityQueryEnumerator<BkmCapturePointComponent, SpriteComponent, TransformComponent>();

        while (enumerator.MoveNext(out var uid, out var comp, out var sprite, out var xform))
        {
            if (xform.MapID != args.MapId)
                continue;

            //if (comp.CaptureMax == comp.CaptureCurrent || comp.CaptureCurrent == 0) // no moving
            //    continue;

            var worldPosition = _transform.GetWorldPosition(xform, xformQuery);
            if (!bounds.Contains(worldPosition))
                continue;

            if (uid != localEnt)
                handle.UseShader(null);
            else
                handle.UseShader(_unshadedShader);

            var worldMatrix = Matrix3Helpers.CreateTranslation(worldPosition);
            var scaledWorld = Matrix3x2.Multiply(scaleMatrix, worldMatrix);
            var matty = Matrix3x2.Multiply(rotationMatrix, scaledWorld);
            handle.SetTransform(matty);

            var offset = 0f;

            // Use the sprite itself if we know its bounds. This means short or tall sprites don't get overlapped
            // by the bar.
            float yOffset = sprite.Bounds.Height / 2f + 0.05f;

            // Position above the entity (we've already applied the matrix transform to the entity itself)
            // Offset by the texture size for every do_after we have.
            var position = new Vector2(-_barTexture.Width / 2f / EyeManager.PixelsPerMeter,
                yOffset / scale + offset / EyeManager.PixelsPerMeter * scale);

            // Draw the underlying bar texture
            handle.DrawTexture(_barTexture, position);

            var elapsedRatio = (float) FixedPoint2.Min(1, comp.CaptureCurrent / comp.CaptureMax);
            var color = GetProgressColor(comp.Team,elapsedRatio, 1);

            var xProgress = (EndX - StartX) * elapsedRatio + StartX;
            var box = new Box2(new Vector2(StartX, 3f) / EyeManager.PixelsPerMeter, new Vector2(xProgress, 4f) / EyeManager.PixelsPerMeter);
            box = box.Translated(position);
            handle.DrawRect(box, color);
        }

        handle.UseShader(null);
        handle.SetTransform(Matrix3x2.Identity);
    }

    public Color GetProgressColor(StationTeamMarker team, float progress, float alpha = 1f)
    {
        var color = team switch
        {
            StationTeamMarker.TeamA => Color.DarkRed,
            StationTeamMarker.TeamB => Color.DarkBlue,
            _ => Color.DarkGray
        };

        return color.WithAlpha(alpha);
    }
}
