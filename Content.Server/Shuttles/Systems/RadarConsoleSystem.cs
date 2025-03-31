using System.Linq;
using System.Numerics;
using Content.Server.Shuttles.Components;
using Content.Shared.Shuttles.BUIStates;
using Content.Shared.Shuttles.Components;
using Content.Shared.Shuttles.Systems;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Server.Shuttles.Systems;

public sealed class RadarConsoleSystem : SharedRadarConsoleSystem
{
    [Dependency] private readonly IGameTiming _timing = default!;

    [Dependency] private readonly SharedTransformSystem _transform = default!;

    [Dependency] private readonly ShuttleConsoleSystem _console = default!;
    [Dependency] private readonly UserInterfaceSystem _uiSystem = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RadarConsoleComponent, ComponentStartup>(OnRadarStartup);
    }

    private void OnRadarStartup(EntityUid uid, RadarConsoleComponent component, ComponentStartup args)
    {
        UpdateState(uid, component);
    }

    // backmen edit start
    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var radars = EntityQueryEnumerator<RadarConsoleComponent>();
        while (radars.MoveNext(out var uid, out var radarComp))
        {
            if (!_uiSystem.HasUi(uid, RadarConsoleUiKey.Key))
                continue;

            // ENEMY PROJECTILE ETA _timing.CurTime - radarComp.NextDetectableUpdate!!!!!! IT'S OVER!!!!
            if (radarComp.NextDetectableUpdate >= _timing.CurTime)
                continue;

            radarComp.NextDetectableUpdate = _timing.CurTime + radarComp.DetectablesUpdateRate;
            UpdateUi((uid, radarComp));
        }
    }

    public bool CanBeSpotted(EntityUid spotter,
        EntityUid spotted,
        RadarConsoleComponent? radarConsole = null,
        BkmRadarDetectableComponent? detectable = null)
    {
        if (!Resolve(spotter, ref radarConsole)
            || !Resolve(spotted, ref detectable))
            return false;

        if (!detectable.Spottable)
            return false;

        var beforeSpottedEvent = new SpottingAttemptEvent((spotter, radarConsole));
        RaiseLocalEvent(spotted, ref beforeSpottedEvent);

        if (beforeSpottedEvent.Cancelled)
            return false;

        var detectableSpottingDistance = detectable.SpottingDistance * radarConsole.SpottingMultiplier;
        var distance = _transform.GetWorldPosition(spotter) - _transform.GetWorldPosition(spotted);

        if (distance.EqualsApprox(Vector2.Zero))
            distance = new Vector2(0.01f, 0);

        if (distance.Length() > detectableSpottingDistance)
            return false;

        return true;
    }

    public void SetSpottableValue(EntityUid spottable, bool value, BkmRadarDetectableComponent? detectable = null)
    {
        if (!Resolve(spottable, ref detectable))
            return;

        detectable.Spottable = value;
    }

    public void SetSpottableDrawType(EntityUid spottable, DetectableDrawType drawType, BkmRadarDetectableComponent? detectable = null)
    {
        if (!Resolve(spottable, ref detectable))
            return;

        detectable.DrawType = drawType;
    }

    public void SetSpottableColor(EntityUid spottable, Color color, BkmRadarDetectableComponent? detectable = null)
    {
        if (!Resolve(spottable, ref detectable))
            return;

        detectable.DetectableColor = color;
    }

    public void SetSpottableMetadata(
        EntityUid spottable,
        float size,
        string? name = null,
        BkmRadarDetectableComponent? detectable = null)
    {
        if (!Resolve(spottable, ref detectable))
            return;

        detectable.DetectableSize = size;
        detectable.RadarName = name ?? detectable.RadarName;
    }

    public void SetSpottableMetadata(
        EntityUid spottable,
        string name,
        float? size,
        BkmRadarDetectableComponent? detectable = null)
    {
        if (!Resolve(spottable, ref detectable))
            return;

        detectable.DetectableSize = size ?? detectable.DetectableSize;
        detectable.RadarName = name;
    }
    // backmen edit end

    private void UpdateUi(Entity<RadarConsoleComponent> radar)
    {
        var xform = Transform(radar);
        var onGrid = xform.ParentUid == xform.GridUid;
        EntityCoordinates? coordinates = onGrid ? xform.Coordinates : null;
        Angle? angle = onGrid ? xform.LocalRotation : null;

        if (radar.Comp.FollowEntity)
        {
            coordinates = new EntityCoordinates(radar, Vector2.Zero);
            angle = Angle.Zero;
        }

        if (_uiSystem.HasUi(radar, RadarConsoleUiKey.Key))
        {
            NavInterfaceState state;
            var docks = _console.GetAllDocks();
            var detectables = _console.GetAllDetectables(radar).ToList(); // backmen edit

            if (coordinates != null && angle != null)
            {
                state = _console.GetNavState(radar.Owner, docks, detectables, coordinates.Value, angle.Value);
            }
            else
            {
                state = _console.GetNavState(radar.Owner, docks, detectables);
            }

            state.RotateWithEntity = !radar.Comp.FollowEntity;

            _uiSystem.SetUiState(radar.Owner, RadarConsoleUiKey.Key, new NavBoundUserInterfaceState(state));
        }
    }

    protected override void UpdateState(EntityUid uid, RadarConsoleComponent component)
    {
        UpdateUi((uid, component));
    }
}
