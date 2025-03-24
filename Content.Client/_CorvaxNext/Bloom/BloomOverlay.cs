using Content.Shared._CorvaxNext.Bloom;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;
using System.Numerics;

namespace Content.Client._CorvaxNext.Bloom;

public sealed class BloomOverlay : Overlay
{
    [Dependency] private readonly IEntityManager _entity = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    private SharedTransformSystem? _transform = null;

    public const int MaxCount = 32;

    private const float MaxDistance = 20f;

    public override OverlaySpace Space => OverlaySpace.WorldSpace;
    public override bool RequestScreenTexture => true;

    private readonly ShaderInstance _shader;

    public BloomOverlay()
    {
        IoCManager.InjectDependencies(this);
        _shader = _prototype.Index<ShaderPrototype>("Bloom").Instance().Duplicate();
        _shader.SetParameter("maxDistance", MaxDistance * EyeManager.PixelsPerMeter);
    }

    private readonly Vector2[] _positions = new Vector2[MaxCount];
    private readonly float[] _brightness = new float[MaxCount];
    private readonly Vector2[] _color1 = new Vector2[MaxCount];
    private readonly Vector2[] _color2 = new Vector2[MaxCount];
    private int _count = 0;

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        if (args.Viewport.Eye is null)
            return false;

        if (_transform is null && !_entity.TrySystem(out _transform))
            return false;

        _count = 0;
        var query = _entity.EntityQueryEnumerator<BloomComponent, PointLightComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var bloom, out var light, out var transform))
        {
            if (transform.MapID != args.MapId)
                continue;

            if (!light.Enabled)
                continue;

            var mapPos = _transform.GetWorldPosition(uid);

            mapPos += _transform.GetWorldRotation(uid).RotateVec(new(0, 6.5f / 16));

            if ((mapPos - args.WorldAABB.ClosestPoint(mapPos)).LengthSquared() > MaxDistance * MaxDistance)
                continue;

            var tempCoords = args.Viewport.WorldToLocal(mapPos);
            tempCoords.Y = args.Viewport.Size.Y - tempCoords.Y;

            _positions[_count] = tempCoords;
            _brightness[_count] = -1 / bloom.Brightness;
            _color1[_count] = new(bloom.Color.R * 10, bloom.Color.G * 10);
            _color2[_count] = new(bloom.Color.B * 10, bloom.Color.A);

            _count++;

            if (_count >= MaxCount)
                break;
        }

        return _count > 0;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (ScreenTexture is null || args.Viewport.Eye is null)
            return;

        _shader?.SetParameter("SCREEN_TEXTURE", ScreenTexture);
        _shader?.SetParameter("renderScale", args.Viewport.Eye.Zoom / args.Viewport.RenderScale);
        _shader?.SetParameter("count", _count);
        _shader?.SetParameter("position", _positions);
        _shader?.SetParameter("brightness", _brightness);
        _shader?.SetParameter("color1", _color1);
        _shader?.SetParameter("color2", _color2);

        args.WorldHandle.UseShader(_shader);
        args.WorldHandle.DrawRect(args.WorldAABB, Color.White);
        args.WorldHandle.UseShader(null);
    }
}
