using Content.Server.Atmos.Monitor.Components;
using Content.Server.Atmos.Monitor.Systems;
using Content.Server.Atmos.Piping.Binary.Components;
using Content.Server.Atmos.Piping.EntitySystems;
using Content.Server.Atmos.Piping.Unary.Components;
using Content.Shared.Atmos.Components;
using Content.Shared.Atmos.Monitor;
using Content.Shared.Atmos.Piping.Binary.Components;
using Content.Shared.Power.Components;
using Content.Shared.Power.EntitySystems;
using Content.Shared.Procedural;
using Content.Shared.Tag;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Server.Backmen.Procedural;

public sealed partial class RoomSetupSystem : EntitySystem
{
    [Dependency] private AtmosDeviceSystem _atmosDevice = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedMapSystem _maps = default!;
    [Dependency] private SharedPowerReceiverSystem _powerReceiver = default!;
    [Dependency] private TagSystem _tag = default!;

    private EntityQuery<GasPortableComponent> _portableQuery;
    private EntityQuery<RoomSetupGatedComponent> _gatedQuery;

    public override void Initialize()
    {
        base.Initialize();

        _portableQuery = GetEntityQuery<GasPortableComponent>();
        _gatedQuery = GetEntityQuery<RoomSetupGatedComponent>();

        SubscribeLocalEvent<AnchorStateChangedEvent>(OnAnchorChanged);
    }

    private void OnAnchorChanged(ref AnchorStateChangedEvent args)
    {
        if (!_portableQuery.HasComponent(args.Entity))
            return;

        CheckAllZones();
    }

    /// <summary>
    /// Called by <see cref="Procedural.RoomFillSystem"/> after a room template is spawned.
    /// </summary>
    public void InitializeZone(
        EntityUid marker,
        RoomSetupZoneComponent zone,
        EntityUid gridUid,
        MapGridComponent grid,
        Vector2i origin,
        DungeonRoomPrototype room)
    {
        zone.GridUid = gridUid;
        zone.Bounds = new Box2i(origin, origin + room.Size - Vector2i.One);
        zone.PortEntities.Clear();

        var bounds = new Box2(zone.Bounds.BottomLeft, zone.Bounds.TopRight + Vector2i.One);
        foreach (var entity in _lookup.GetEntitiesIntersecting(gridUid, bounds, LookupFlags.Static | LookupFlags.Sundries))
        {
            if (HasComp<GasPortComponent>(entity))
                zone.PortEntities.Add(entity);
        }

        if (zone.PortEntities.Count == 0)
            Log.Warning($"Room setup zone {ToPrettyString(marker)} found no gas ports in {zone.Bounds}");

        GateEntitiesInZone((marker, zone));
        TryActivateZone((marker, zone));
    }

    private void GateEntitiesInZone(Entity<RoomSetupZoneComponent> zone)
    {
        if (zone.Comp.GridUid == null)
            return;

        foreach (var entity in GetGatedEntities(zone))
        {
            DisableGatedEntity(entity);
        }
    }

    private void CheckAllZones()
    {
        var query = EntityQueryEnumerator<RoomSetupZoneComponent>();
        while (query.MoveNext(out var uid, out var zone))
        {
            if (zone.Activated)
                continue;

            TryActivateZone((uid, zone));
        }
    }

    private void TryActivateZone(Entity<RoomSetupZoneComponent> zone)
    {
        if (zone.Comp.Activated)
            return;

        if (zone.Comp.Requirements.HasFlag(RoomSetupRequirements.Canisters) && !AreCanistersConnected(zone))
            return;

        ActivateZone(zone);
    }

    private bool AreCanistersConnected(Entity<RoomSetupZoneComponent> zone)
    {
        if (zone.Comp.PortEntities.Count < zone.Comp.RequiredCanisterPorts)
            return false;

        var connected = 0;

        foreach (var port in zone.Comp.PortEntities)
        {
            if (!TryComp(port, out TransformComponent? portXform) || portXform.GridUid == null)
                continue;

            if (!TryComp(portXform.GridUid.Value, out MapGridComponent? grid))
                continue;

            foreach (var entity in _maps.GetAnchoredEntities(portXform.GridUid.Value, grid, portXform.Coordinates))
            {
                if (!_portableQuery.HasComponent(entity))
                    continue;

                connected++;
                break;
            }
        }

        return connected >= zone.Comp.RequiredCanisterPorts;
    }

    private IEnumerable<EntityUid> GetGatedEntities(Entity<RoomSetupZoneComponent> zone)
    {
        if (zone.Comp.GridUid == null)
            yield break;

        var bounds = new Box2(zone.Comp.Bounds.BottomLeft, zone.Comp.Bounds.TopRight + Vector2i.One);
        foreach (var entity in _lookup.GetEntitiesIntersecting(zone.Comp.GridUid.Value, bounds))
        {
            if (!_gatedQuery.HasComponent(entity))
                continue;

            if (!_tag.HasTag(entity, zone.Comp.ActivationTag))
                continue;

            yield return entity;
        }
    }

    private void ActivateZone(Entity<RoomSetupZoneComponent> zone)
    {
        zone.Comp.Activated = true;

        foreach (var entity in GetGatedEntities(zone))
        {
            ActivateGatedEntity(entity);
        }

        QueueDel(zone);
    }

    private void DisableGatedEntity(EntityUid uid)
    {
        if (TryComp(uid, out AtmosDeviceComponent? device))
            _atmosDevice.LeaveAtmosphere((uid, device));
    }

    private void ActivateGatedEntity(EntityUid uid)
    {
        if (!TryComp(uid, out RoomSetupGatedComponent? gated) || gated.Activated)
            return;

        gated.Activated = true;
        Dirty(uid, gated);

        if (TryComp(uid, out AirAlarmComponent? alarm))
        {
            alarm.AutoMode = true;
            Dirty(uid, alarm);
        }

        if (TryComp(uid, out GasVentPumpComponent? pump))
        {
            pump.Enabled = true;
            Dirty(uid, pump);
        }

        if (TryComp(uid, out GasVolumePumpComponent? volumePump))
        {
            volumePump.Enabled = true;
            Dirty(uid, volumePump);
        }

        if (TryComp(uid, out ApcPowerReceiverComponent? receiver))
            _powerReceiver.SetPowerDisabled(uid, false, receiver);

        if (TryComp(uid, out AtmosDeviceComponent? device))
            _atmosDevice.JoinAtmosphere((uid, device));

        if (TryComp(uid, out GasVentScrubberComponent? _))
        {
            var ev = new AtmosAlarmEvent(AtmosAlarmType.Normal);
            RaiseLocalEvent(uid, ev);
        }
    }
}
