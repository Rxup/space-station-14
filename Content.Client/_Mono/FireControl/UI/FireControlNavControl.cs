using Content.Client._Mono.Radar;
using Content.Client.Shuttles.UI;
using Content.Shared._Mono.FireControl;
using Content.Shared._Mono.Radar;
using Content.Shared.Physics;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.UserInterface;
using Robust.Shared.Input;
using Robust.Shared.Map;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Timing;
using System.Linq;
using System.Numerics;

namespace Content.Client._Mono.FireControl.UI;

public sealed partial class FireControlNavControl : ShuttleNavControl
{
    [Dependency] private IInputManager _input = default!;
    private readonly SharedTransformSystem _transform;
    private readonly SharedPhysicsSystem _physics;
    private readonly RadarBlipsSystem _blips;

    private EntityUid? _activeConsole;
    private FireControllableEntry[]? _controllables;
    private HashSet<NetEntity> _selectedWeapons = new();

    private float _lastCursorUpdateTime;
    private float _lastFireTime;
    private const float CursorUpdateInterval = 0.1f;
    private const float FireRateLimit = 0.1f;

    public FireControlNavControl()
    {
        IoCManager.InjectDependencies(this);
        WorldMaxRange = 512f;
        ActualRadarRange = 512f;
        _blips = EntManager.System<RadarBlipsSystem>();
        _physics = EntManager.System<SharedPhysicsSystem>();
        _transform = EntManager.System<SharedTransformSystem>();

        OnMouseEntered += _ => _isMouseInside = true;
        OnMouseExited += _ => _isMouseInside = false;
    }

    protected override void KeyBindDown(GUIBoundKeyEventArgs args)
    {
        base.KeyBindDown(args);

        if (args.Function != EngineKeyFunctions.UIClick)
            return;

        _isMouseDown = true;
        // Pixel space matches DrawingHandleScreen + MidPoint/shuttleToView draw coords.
        _lastMousePos = args.RelativePixelPosition;
        TryFireAtPosition(_lastMousePos);
    }

    protected override void KeyBindUp(GUIBoundKeyEventArgs args)
    {
        if (args.Function == EngineKeyFunctions.UIClick)
            _isMouseDown = false;

        base.KeyBindUp(args);
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        if (!_isMouseDown || !_isMouseInside)
            return;

        var currentTime = IoCManager.Resolve<IGameTiming>().CurTime.TotalSeconds;
        if (currentTime - _lastFireTime < FireRateLimit)
            return;

        _lastMousePos = GetLocalPosition(_input.MouseScreenPosition);
        TryFireAtPosition(_lastMousePos);
        _lastFireTime = (float)currentTime;
    }

    protected override void MouseMove(GUIMouseMoveEventArgs args)
    {
        base.MouseMove(args);
        _lastMousePos = args.RelativePixelPosition;
        if (_isMouseInside)
            TryUpdateCursorPosition(_lastMousePos);
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        if (_coordinates == null || _rotation == null)
            return;

        var xformQuery = EntManager.GetEntityQuery<TransformComponent>();
        if (!xformQuery.TryGetComponent(_coordinates.Value.EntityId, out var xform)
            || xform.MapID == MapId.Nullspace)
        {
            return;
        }

        base.Draw(handle);

        var posMatrix = Matrix3Helpers.CreateTransform(_coordinates.Value.Position, _rotation.Value);
        var ourEntRot = RotateWithEntity ? _transform.GetWorldRotation(xform) : _rotation.Value;
        var ourEntMatrix = Matrix3Helpers.CreateTransform(_transform.GetWorldPosition(xform), ourEntRot);
        var shuttleToWorld = Matrix3x2.Multiply(posMatrix, ourEntMatrix);
        Matrix3x2.Invert(shuttleToWorld, out var worldToShuttle);
        var shuttleToView = Matrix3x2.CreateScale(new Vector2(MinimapScale, -MinimapScale))
            * Matrix3x2.CreateTranslation(MidPointVector);
        Matrix3x2.Invert(worldToShuttle * shuttleToView, out var viewToWorld);

        var blips = _blips.GetCurrentBlips();
        var colors = new Dictionary<NetEntity, Color>();
        foreach (var blip in blips)
            colors[blip.NetUid] = blip.Color;

        if (_controllables == null || !_isMouseInside)
            return;

        // Keep live cursor in pixel space so the line tracks the OS cursor at UIScale ≠ 1.
        var mousePixelPos = GetLocalPosition(_input.MouseScreenPosition);
        _lastMousePos = mousePixelPos;

        foreach (var controllable in _controllables)
        {
            if (!_selectedWeapons.Contains(controllable.NetEntity))
                continue;

            var coords = EntManager.GetCoordinates(controllable.Coordinates);
            var worldPos = _transform.ToMapCoordinates(coords).Position;

            // mousePixelPos is in the same numeric space as shuttleToView draw output
            // (MidPoint already includes UIScale; draw uses those values as pixels).
            var cursorWorldPos = Vector2.Transform(mousePixelPos, viewToWorld);

            var direction = cursorWorldPos - worldPos;
            if (direction.LengthSquared() < float.Epsilon)
                continue;

            var ray = new CollisionRay(worldPos, direction.Normalized(), (int)CollisionGroup.Impassable);

            var results = _physics.IntersectRay(xform.MapID, ray, direction.Length(), ignoredEnt: _coordinates?.EntityId);

            if (!results.Any() && colors.TryGetValue(controllable.NetEntity, out var color))
            {
                var weaponViewPos = TryGetBlipViewPosition(controllable.NetEntity, blips, worldToShuttle, shuttleToView)
                    ?? WorldToViewPosition(worldPos, worldToShuttle, shuttleToView);
                handle.DrawLine(weaponViewPos, mousePixelPos, color.WithAlpha(0.3f));
            }
        }
    }

    private Vector2? TryGetBlipViewPosition(
        NetEntity entity,
        List<(NetEntity NetUid, EntityCoordinates Position, float Scale, Color Color, RadarBlipShape Shape, bool SonarEcho)> blips,
        Matrix3x2 worldToShuttle,
        Matrix3x2 shuttleToView)
    {
        foreach (var blip in blips)
        {
            if (blip.NetUid != entity)
                continue;

            var blipMap = _transform.ToMapCoordinates(blip.Position);
            return WorldToViewPosition(blipMap.Position, worldToShuttle, shuttleToView);
        }

        return null;
    }

    public void UpdateControllables(EntityUid console, FireControllableEntry[] controllables)
    {
        _activeConsole = console;
        _controllables = controllables;
    }

    public void UpdateSelectedWeapons(HashSet<NetEntity> selectedWeapons)
    {
        _selectedWeapons = selectedWeapons;
    }

    private void TryUpdateCursorPosition(Vector2 pixelPosition)
    {
        var currentTime = IoCManager.Resolve<IGameTiming>().CurTime.TotalSeconds;
        if (currentTime - _lastCursorUpdateTime < CursorUpdateInterval)
            return;

        _lastCursorUpdateTime = (float)currentTime;
        TryFireAtPosition(pixelPosition);
    }

    /// <summary>
    /// <paramref name="pixelPosition"/> must be control-relative real pixels
    /// (<see cref="GUIBoundKeyEventArgs.RelativePixelPosition"/> / <see cref="Control.GetLocalPosition"/>),
    /// matching DrawingHandleScreen and MidPoint/shuttleToView draw coordinates.
    /// </summary>
    private void TryFireAtPosition(Vector2 pixelPosition)
    {
        if (_coordinates == null || _rotation == null || OnRadarClick == null)
            return;

        var a = InverseScalePosition(pixelPosition);
        var relativeWorldPos = new Vector2(a.X, -a.Y);
        relativeWorldPos = _rotation.Value.RotateVec(relativeWorldPos);
        var coords = _coordinates.Value.Offset(relativeWorldPos);

        OnRadarClick.Invoke(coords);
    }

    public bool IsMouseDown() => _isMouseDown;
}
