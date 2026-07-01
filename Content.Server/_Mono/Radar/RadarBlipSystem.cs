using System.Numerics;
using Content.Shared._Lua.Shuttles.Components;
using Content.Shared._Mono.Radar;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Shuttles.Components;
using Content.Shared.Shuttles.Systems;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;

namespace Content.Server._Mono.Radar;

public sealed partial class RadarBlipSystem : EntitySystem
{
    [Dependency] private SharedTransformSystem _xform = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<RequestBlipsEvent>(OnBlipsRequested);
        SubscribeLocalEvent<RadarBlipComponent, ComponentShutdown>(OnBlipShutdown);
    }
    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var mobQuery = EntityQueryEnumerator<MobStateComponent>();
        while (mobQuery.MoveNext(out var mobUid, out var mobState))
        {
            var isDead = mobState.CurrentState == MobState.Dead;
            if (isDead)
            {
                if (!HasComp<RadarBlipComponent>(mobUid))
                {
                    var rb = EnsureComp<RadarBlipComponent>(mobUid);
                    rb.VisibleFromOtherGrids = true;
                    rb.RequireNoGrid = false;
                    rb.RadarColor = Color.Red;
                    rb.Scale = 0f;
                    rb.Enabled = true;
                    rb.MaxDistance = 512f;
                    var icon = EnsureComp<RadarBlipIconComponent>(mobUid);
                    icon.Icon = new Robust.Shared.Utility.ResPath("/Textures/_Lua/Interface/Radar/dead_cross.png");
                    icon.Scale = 3.5f;
                }
            }
            else
            {
                RemComp<RadarBlipComponent>(mobUid);
                RemComp<RadarBlipIconComponent>(mobUid);
            }
        }
    }

    private void OnBlipsRequested(RequestBlipsEvent ev, EntitySessionEventArgs args)
    {
        if (!TryGetEntity(ev.Radar, out var radarUid))
            return;

        if (!TryComp<RadarConsoleComponent>(radarUid, out var radar))
            return;


        var blips = AssembleBlipsReport((EntityUid)radarUid, radar);
        var hitscans = AssembleHitscanReport((EntityUid)radarUid, radar);

        // Combine the blips and hitscan lines
        var giveEv = new GiveBlipsEvent(blips, hitscans);
        RaiseNetworkEvent(giveEv, args.SenderSession);

        blips.Clear();
        hitscans.Clear();
    }

    private void OnBlipShutdown(EntityUid blipUid, RadarBlipComponent component, ComponentShutdown args)
    {
        var netBlipUid = GetNetEntity(blipUid);
        var removalEv = new BlipRemovalEvent(netBlipUid);
        RaiseNetworkEvent(removalEv);
    }

    private List<(NetEntity netUid, NetCoordinates Position, Vector2 Vel, float Scale, Color Color, RadarBlipShape Shape, bool SonarEcho)> AssembleBlipsReport(EntityUid uid, RadarConsoleComponent? component = null)
    {
        var blips = new List<(NetEntity netUid, NetCoordinates Position, Vector2 Vel, float Scale, Color Color, RadarBlipShape Shape, bool SonarEcho)>();

        if (Resolve(uid, ref component))
        {
            var radarXform = Transform(uid);
            var radarPosition = _xform.GetWorldPosition(uid);
            var radarGrid = _xform.GetGrid(uid);
            var radarMapId = radarXform.MapID;

            var blipQuery = EntityQueryEnumerator<RadarBlipComponent, TransformComponent, PhysicsComponent>();

            while (blipQuery.MoveNext(out var blipUid, out var blip, out var blipXform, out var blipPhysics))
            {
                if (!blip.Enabled)
                    continue;

                // This prevents blips from showing on radars that are on different maps
                if (blipXform.MapID != radarMapId)
                    continue;

                var netBlipUid = GetNetEntity(blipUid);

                var blipGrid = blipXform.GridUid;

                var blipVelocity = _physics.GetMapLinearVelocity(blipUid, blipPhysics, blipXform);

                var distance = (_xform.GetWorldPosition(blipXform) - radarPosition).Length();

                float maxDistance = blip.MaxDistance;
                var radarMax = component?.MaxRange ?? SharedRadarConsoleSystem.DefaultMaxRange;
                var allowedDistance = Math.Min(maxDistance, radarMax);
                if (distance > allowedDistance) continue;
                if ((blip.RequireNoGrid && blipGrid != null) || (!blip.VisibleFromOtherGrids && blipGrid != radarGrid)) continue;

                // due to PVS being a thing, things will break if we try to parent to not the map or a grid
                var coord = blipXform.Coordinates;
                if (blipXform.ParentUid != blipXform.MapUid && blipXform.ParentUid != blipGrid)
                    coord = _xform.WithEntityId(coord, blipGrid ?? blipXform.MapUid!.Value);
                // we're parented to either the map or a grid and this is relative velocity so account for grid movement
                if (blipGrid != null)
                    blipVelocity -= _physics.GetLinearVelocity(blipGrid.Value, coord.Position);

                var sonarEcho = HasComp<RadarSonarEchoComponent>(blipUid);
                blips.Add((netBlipUid, GetNetCoordinates(coord), blipVelocity, blip.Scale, blip.RadarColor, blip.Shape, sonarEcho));
            }
        }

        return blips;
    }

    /// <summary>
    /// Assembles trajectory information for hitscan projectiles to be displayed on radar
    /// </summary>
    private List<(Vector2 Start, Vector2 End, float Thickness, Color Color)> AssembleHitscanReport(EntityUid uid, RadarConsoleComponent? component = null)
    {
        var hitscans = new List<(Vector2 Start, Vector2 End, float Thickness, Color Color)>();

        if (!Resolve(uid, ref component))
            return hitscans;

        var radarPosition = _xform.GetWorldPosition(uid);

        var hitscanQuery = EntityQueryEnumerator<HitscanRadarComponent>();

        while (hitscanQuery.MoveNext(out var hitscanUid, out var hitscan))
        {
            if (!hitscan.Enabled)
                continue;

            // Check if either the start or end point is within radar range
            var startDistance = (hitscan.StartPosition - radarPosition).Length();
            var endDistance = (hitscan.EndPosition - radarPosition).Length();

            if (startDistance > component.MaxRange && endDistance > component.MaxRange)
                continue;

            hitscans.Add((hitscan.StartPosition, hitscan.EndPosition, hitscan.LineThickness, hitscan.RadarColor));
        }

        return hitscans;
    }

}
