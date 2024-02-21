using System.Numerics;
using Content.Client.Parallax;
using Content.Client.Weather;
using Content.Shared.Backmen.StationAI;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client.Backmen.StationAI;

public sealed class AiCamOverlay : Overlay
{
    [Dependency] private readonly IClyde _clyde = default!;
    [Dependency] private readonly IEntityManager _entManager = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly IPrototypeManager _protoManager = default!;
    [Dependency] private readonly IPlayerManager _player = default!;

    private readonly ParallaxSystem _parallax;
    private readonly SharedTransformSystem _transform;
    private readonly SpriteSystem _sprite;
    private readonly StationAiSystem _aiSystem;


    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    private IRenderTexture? _blep;

    private readonly ShaderInstance _shader;

    public AiCamOverlay(ParallaxSystem parallax, SharedTransformSystem transform, SpriteSystem sprite, StationAiSystem aiSystem)
    {
        ZIndex = ParallaxSystem.ParallaxZIndex + 1;
        _parallax = parallax;
        _transform = transform;
        _sprite = sprite;
        _aiSystem = aiSystem;
        IoCManager.InjectDependencies(this);
        _shader = _protoManager.Index<ShaderPrototype>("WorldGradientCircle").InstanceUnique();
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var playerUid = _player.LocalSession?.AttachedEntity;
        if (playerUid is not { Valid: true })
            return;

        if (!_entManager.TryGetComponent<AIEyeComponent>(playerUid, out var eyeComponent))
        {
            return;
        }

        var invMatrix = args.Viewport.GetWorldToLocalMatrix();

        if (_blep?.Texture.Size != args.Viewport.Size)
        {
            _blep?.Dispose();
            _blep = _clyde.CreateRenderTarget(args.Viewport.Size, new RenderTargetFormatParameters(RenderTargetColorFormat.Rgba8Srgb), name: "aicam-stencil");
        }

        if (eyeComponent.Camera != null)
        {
            DrawRestrictedRange(args, eyeComponent, invMatrix);
        }

        args.WorldHandle.UseShader(null);
        args.WorldHandle.SetTransform(Matrix3.Identity);
    }

    private void DrawRestrictedRange(in OverlayDrawArgs args, AIEyeComponent eyeComponent, Matrix3 invMatrix)
    {
        var pos = _transform.GetMapCoordinates(eyeComponent.Camera!.Value);

        var worldHandle = args.WorldHandle;
        var renderScale = args.Viewport.RenderScale.X;
        // TODO: This won't handle non-standard zooms so uhh yeah, not sure how to structure it on the shader side.
        var zoom = args.Viewport.Eye?.Zoom ?? Vector2.One;
        var length = zoom.X;
        var bufferRange = MathF.Min(10f, SharedStationAISystem.CameraEyeRange);

        var pixelCenter = invMatrix.Transform(pos.Position);
        // Something something offset?
        var vertical = args.Viewport.Size.Y;

        var pixelMaxRange = SharedStationAISystem.CameraEyeRange * renderScale / length * EyeManager.PixelsPerMeter;
        var pixelBufferRange = bufferRange * renderScale / length * EyeManager.PixelsPerMeter;
        var pixelMinRange = pixelMaxRange - pixelBufferRange;

        _shader.SetParameter("position", new Vector2(pixelCenter.X, vertical - pixelCenter.Y));
        _shader.SetParameter("maxRange", pixelMaxRange);
        _shader.SetParameter("minRange", pixelMinRange);
        _shader.SetParameter("bufferRange", pixelBufferRange);
        _shader.SetParameter("gradient", 0.80f);

        var worldAABB = args.WorldAABB;
        var worldBounds = args.WorldBounds;
        var position = args.Viewport.Eye?.Position.Position ?? Vector2.Zero;
        var localAABB = invMatrix.TransformBox(worldAABB);

        // Cut out the irrelevant bits via stencil
        // This is why we don't just use parallax; we might want specific tiles to get drawn over
        // particularly for planet maps or stations.
        worldHandle.RenderInRenderTarget(_blep!, () =>
        {
            worldHandle.UseShader(_shader);
            worldHandle.DrawRect(localAABB, Color.White);
        }, Color.Transparent);

        worldHandle.SetTransform(Matrix3.Identity);
        worldHandle.UseShader(_protoManager.Index<ShaderPrototype>("StencilMask").Instance());
        worldHandle.DrawTextureRect(_blep!.Texture, worldBounds);
        var curTime = _timing.RealTime;
        var sprite = _sprite.GetFrame(new SpriteSpecifier.Texture(new ResPath("/Textures/Parallaxes/aicam.png")), curTime);

        // Draw the fog
        worldHandle.UseShader(_protoManager.Index<ShaderPrototype>("StencilDraw").Instance());
        _parallax.DrawParallax(worldHandle, worldAABB, sprite, curTime, position, new Vector2(0.5f, 0f));
    }
}
