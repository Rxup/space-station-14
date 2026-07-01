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
using System.Linq; // Lua
using System.Numerics; // Lua

namespace Content.Client._Mono.FireControl.UI;

public sealed class FireControlNavControl : ShuttleNavControl
{
    [Dependency] private readonly IInputManager _input = default!;
    private readonly SharedTransformSystem _transform;
    private readonly SharedPhysicsSystem _physics;
    private readonly RadarBlipsSystem _blips;

    private EntityUid? _activeConsole;
    private FireControllableEntry[]? _controllables;
    private HashSet<NetEntity> _selectedWeapons = new();

    // Add a limit to how often we update the cursor position to prevent network spam
    private float _lastCursorUpdateTime = 0f;
    private float _lastFireTime = 0f;
    private const float CursorUpdateInterval = 0.1f; // 10 updates per second
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
        _lastMousePos = args.RelativePosition;
        TryFireAtPosition(args.RelativePosition);
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

        var mousePos = UserInterfaceManager.MousePositionScaled;
        var relativePos = mousePos.Position - GlobalPosition;
        _lastMousePos = relativePos;
        TryFireAtPosition(relativePos);
        _lastFireTime = (float)currentTime;
    }

    protected override void MouseMove(GUIMouseMoveEventArgs args)
    {
        base.MouseMove(args);
        _lastMousePos = args.RelativePosition;
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
        var worldToView = worldToShuttle * shuttleToView;
        Matrix3x2.Invert(worldToView, out var viewToWorld);

        var blips = _blips.GetCurrentBlips();
        var colors = new Dictionary<NetEntity, Color>();
        foreach (var blip in blips)
            colors[blip.NetUid] = blip.Color;

        if (_controllables == null || !_isMouseInside)
            return;

        // Same as ShuttleMapControl FTL preview: physical pixel mouse position on the control.
        var mouseLocalPos = GetLocalPosition(_input.MouseScreenPosition);

        foreach (var controllable in _controllables)
        {
            if (!_selectedWeapons.Contains(controllable.NetEntity))
                continue;

            var coords = EntManager.GetCoordinates(controllable.Coordinates);
            var worldPos = _transform.ToMapCoordinates(coords).Position;

            var cursorWorldPos = Vector2.Transform(_lastMousePos, viewToWorld);

            var direction = cursorWorldPos - worldPos;
            var ray = new CollisionRay(worldPos, direction.Normalized(), (int)CollisionGroup.Impassable);

            var results = _physics.IntersectRay(xform.MapID, ray, direction.Length(), ignoredEnt: _coordinates?.EntityId);

            if (!results.Any() && colors.TryGetValue(controllable.NetEntity, out var color))
            {
                var weaponViewPos = TryGetBlipViewPosition(controllable.NetEntity, blips, worldToShuttle, shuttleToView)
                    ?? WorldToViewPosition(worldPos, worldToShuttle, shuttleToView);
                handle.DrawLine(weaponViewPos, mouseLocalPos, color.WithAlpha(0.3f));
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

    private void TryUpdateCursorPosition(Vector2 relativePosition)
    {
        var currentTime = IoCManager.Resolve<IGameTiming>().CurTime.TotalSeconds;
        if (currentTime - _lastCursorUpdateTime < CursorUpdateInterval)
            return;

        _lastCursorUpdateTime = (float)currentTime;

        if (_coordinates == null || _rotation == null || OnRadarClick == null)
            return;

        var a = InverseScalePosition(relativePosition);
        var relativeWorldPos = new Vector2(a.X, -a.Y);
        relativeWorldPos = _rotation.Value.RotateVec(relativeWorldPos);
        var coords = _coordinates.Value.Offset(relativeWorldPos);

        OnRadarClick?.Invoke(coords);
    }

    private void TryFireAtPosition(Vector2 relativePosition)
    {
        if (_coordinates == null || _rotation == null || OnRadarClick == null)
            return;

        var a = InverseScalePosition(relativePosition);
        var relativeWorldPos = new Vector2(a.X, -a.Y);
        relativeWorldPos = _rotation.Value.RotateVec(relativeWorldPos);
        var coords = _coordinates.Value.Offset(relativeWorldPos);

        OnRadarClick?.Invoke(coords);
    }

    public bool IsMouseDown() => _isMouseDown;
}
