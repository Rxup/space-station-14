using Content.Server.Ghost.Roles;
using Content.Server.Ghost.Roles.Components;
using Content.Shared._Lavaland.LimitedUsage;
using Content.Shared.Backmen.AirDrop;
using Content.Shared.Coordinates.Helpers;
using Content.Shared.EntityTable;
using Content.Shared.Interaction;
using Content.Shared.Storage.EntitySystems;
using Content.Shared.Timing;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Spawners;
using Robust.Shared.Timing;

namespace Content.Server.Backmen.AirDrop;

public sealed partial class AirDropSystem : SharedAirDropSystem
{
    [Dependency] private IPrototypeManager _prototypeManager = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedEntityStorageSystem _storage = default!;
    [Dependency] private EntityTableSystem _entityTable = default!;
    [Dependency] private SharedNoLavalandUsageSystem _noLavaland = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private UseDelaySystem _delay = default!;

    /// <summary>
    /// Buffers work collected during <see cref="EntityQueryEnumerator{TComp1,TComp2}"/>.
    /// Must not <see cref="EntitySystem.Dirty"/> or raise events while iterating — that can loop forever in MoveNext.
    /// </summary>
    private readonly List<EntityUid> _advanceToDrop = new();

    private readonly List<EntityUid> _finishDrop = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<AirDropItemComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<AirDropItemComponent, MapInitEvent>(OnMapInit);

        SubscribeLocalEvent<AirDropComponent, AirDropSpawnEvent>(OnAirDropSpawn);
        SubscribeLocalEvent<AirDropItemSpawnEvent>(OnSpawnLoot);

        SubscribeLocalEvent<AirDropGhostRoleComponent, TakeGhostRoleEvent>(OnTakeOver,
            after: [typeof(GhostRoleSystem)]);

        SubscribeLocalEvent<AirDropComponent, MapInitEvent>(OnInitialStartup);
        SubscribeLocalEvent<AirDropComponent, ComponentShutdown>(OnAirDropShutdown);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        _advanceToDrop.Clear();
        _finishDrop.Clear();

        var curTime = _timing.CurTime;
        var query = EntityQueryEnumerator<AirDropComponent, MetaDataComponent>();

        while (query.MoveNext(out var uid, out var comp, out var meta))
        {
            if (Paused(uid, meta))
                continue;

            if (comp.Phase is AirDropPhase.Inactive or AirDropPhase.Done)
                continue;

            if (curTime < comp.PhaseEndTime)
                continue;

            switch (comp.Phase)
            {
                case AirDropPhase.Target:
                    _advanceToDrop.Add(uid);
                    break;

                case AirDropPhase.Drop:
                    _finishDrop.Add(uid);
                    break;
            }
        }

        foreach (var uid in _advanceToDrop)
        {
            if (!TryComp(uid, out AirDropComponent? comp))
                continue;

            SpawnInAirMarker((uid, comp));
            comp.Phase = AirDropPhase.Drop;
            comp.PhaseEndTime = curTime + TimeSpan.FromSeconds(comp.TimeToDrop);
            DirtyFields(uid, comp, null,
                nameof(AirDropComponent.Phase),
                nameof(AirDropComponent.PhaseEndTime),
                nameof(AirDropComponent.InAirMarker));
        }

        foreach (var uid in _finishDrop)
        {
            if (TerminatingOrDeleted(uid) || !TryComp(uid, out AirDropComponent? comp))
                continue;

            comp.Phase = AirDropPhase.Done;
            comp.PhaseEndTime = TimeSpan.Zero;
            DirtyFields(uid, comp, null,
                nameof(AirDropComponent.Phase),
                nameof(AirDropComponent.PhaseEndTime));

            RaiseLocalEvent(uid, new AirDropSpawnEvent());
        }
    }

    private Filter GetVisualNotifyFilter(EntityUid uid, MapCoordinates coords, float range)
    {
        return Filter.Pvs(Transform(uid).Coordinates, entityMan: EntityManager)
            .AddInRange(coords, range);
    }

    private void BeginAirDrop(Entity<AirDropComponent> ent)
    {
        var curTime = _timing.CurTime;
        ent.Comp.InAirMarker = null;
        ent.Comp.TargetMarker = SpawnMarker(ent.Comp.DropTargetProto, Transform(ent).Coordinates, ent.Comp.DropTarget);
        ent.Comp.Phase = AirDropPhase.Target;
        ent.Comp.PhaseEndTime = curTime + TimeSpan.FromSeconds(ent.Comp.TimeOfTarget);
        DirtyFields(ent, ent.Comp, null,
            nameof(AirDropComponent.Phase),
            nameof(AirDropComponent.PhaseEndTime),
            nameof(AirDropComponent.TargetMarker),
            nameof(AirDropComponent.InAirMarker));
    }

    private void SpawnInAirMarker(Entity<AirDropComponent> ent)
    {
        if (ent.Comp.InAirMarker is { } existing && !TerminatingOrDeleted(existing))
            return;

        if (ent.Comp.TargetMarker is not { } target || TerminatingOrDeleted(target))
            return;

        ent.Comp.InAirMarker = SpawnMarker(ent.Comp.InAirProto, Transform(target).Coordinates, ent.Comp.InAir);
    }

    private EntityUid SpawnMarker(EntProtoId proto, EntityCoordinates coords, ComponentRegistry overrides)
    {
        var uid = Spawn(proto, _transform.ToMapCoordinates(coords), overrides);
        _transform.AttachToGridOrMap(uid);
        return uid;
    }

    private MapCoordinates GetDropCoordinates(AirDropComponent comp)
    {
        if (comp.TargetMarker is { } target && !TerminatingOrDeleted(target))
            return _transform.GetMapCoordinates(target);

        if (comp.InAirMarker is { } inAir && !TerminatingOrDeleted(inAir))
            return _transform.GetMapCoordinates(inAir);

        return MapCoordinates.Nullspace;
    }

    private void CleanupMarkers(AirDropComponent comp)
    {
        if (comp.TargetMarker is { } target)
            QueueDel(target);

        if (comp.InAirMarker is { } inAir)
            QueueDel(inAir);

        comp.TargetMarker = null;
        comp.InAirMarker = null;
    }

    private void OnAirDropShutdown(Entity<AirDropComponent> ent, ref ComponentShutdown args)
    {
        CleanupMarkers(ent.Comp);
    }

    private void OnInitialStartup(Entity<AirDropComponent> ent, ref MapInitEvent args)
    {
        // Loot tables spawn entities before inserting into the pod; those must not start another drop.
        if (_container.IsEntityInContainer(ent.Owner))
            return;

        // Spawning marker entities inside MapInit trips parent-init asserts in EntityManager.
        var uid = ent.Owner;
        Timer.Spawn(TimeSpan.Zero, () => StartAirDrop(uid));
    }

    private void StartAirDrop(EntityUid uid)
    {
        if (TerminatingOrDeleted(uid) || !TryComp(uid, out AirDropComponent? comp))
            return;

        BeginAirDrop((uid, comp));

        if (!TryGetNetEntity(uid, out var airDrop))
            return;

        var coords = GetDropCoordinates(comp);
        if (coords.MapId == MapId.Nullspace)
            coords = _transform.GetMapCoordinates(uid);

        RaiseNetworkEvent(new AirDropStartEvent
            {
                Uid = airDrop.Value,
                Pos = coords
            },
            GetVisualNotifyFilter(uid, coords, comp.VisualNotifyRange)
        );
    }

    private void OnTakeOver(Entity<AirDropGhostRoleComponent> ent, ref TakeGhostRoleEvent args)
    {
        if (!args.TookRole)
            return;

        var xform = Transform(ent.Owner);
        var spawned = SpawnAtPosition(ent.Comp.AfterTakePod, xform.Coordinates);
        if (ent.Comp.SupplyDrop != null)
        {
            var comp = EnsureComp<AirDropVisualizerComponent>(spawned);
            comp.SupplyDrop = ent.Comp.SupplyDrop.Value;
            comp.SupplyDropOverride = ent.Comp.AfterTakePod;
        }
    }

    private void OnAfterInteract(Entity<AirDropItemComponent> ent, ref AfterInteractEvent args)
    {
        if (!args.CanReach)
            return;

        if (!TryComp(ent, out UseDelayComponent? useDelay))
            return;

        if (_delay.IsDelayed((ent, useDelay)))
            return;

        if (ent.Comp.LavaLandOnly && !_noLavaland.IsApply(ent))
            return;

        _delay.TryResetDelay((ent, useDelay));

        var coords = args.ClickLocation;
        if (_transform.GetGrid(coords) is { } gridUid && TryComp<MapGridComponent>(gridUid, out var grid))
            coords = coords.SnapToGrid(grid);

        Spawn(ent.Comp.AirDropProto, coords);

        if (ent.Comp.DeleteOnUse)
        {
            QueueDel(ent);
        }
    }

    private void OnMapInit(Entity<AirDropItemComponent> ent, ref MapInitEvent args)
    {
        if (!TryComp(ent, out UseDelayComponent? useDelay))
            return;
        Entity<UseDelayComponent?> dItem = (ent, useDelay);
        _delay.SetLength(dItem, ent.Comp.Cooldown);

        if (ent.Comp.StartCooldown)
        {
            _delay.TryResetDelay((ent, useDelay));
        }
    }

    private void OnAirDropSpawn(EntityUid uid, AirDropComponent comp, AirDropSpawnEvent args)
    {
        if (args.Handled || TerminatingOrDeleted(uid))
            return;

        var pos = GetDropCoordinates(comp);
        var supply = Spawn(comp.SupplyDropProto, pos, comp.SupplyDrop);
        _transform.AttachToGridOrMap(supply);
        AirDropVisualizerComponent? supplyCompVis = null;
        if (Prototype(uid) is { } proto)
        {
            supplyCompVis = EnsureComp<AirDropVisualizerComponent>(supply);
            supplyCompVis.SupplyDrop = proto.ID;
            if (TryComp<AirDropGhostRoleComponent>(supply, out var dropGhost))
            {
                dropGhost.SupplyDrop = proto.ID;
            }
        }

        CleanupMarkers(comp);
        DirtyFields(uid, comp, null,
            nameof(AirDropComponent.TargetMarker),
            nameof(AirDropComponent.InAirMarker));

        var ev = new TimedDespawnEvent();
        RaiseLocalEvent(uid, ref ev);
        QueueDel(uid);
        args.Handled = true;

        if (comp.SupplyDropTable is { } lootTable)
        {
            var forceOpenSupplyDrop = comp.ForceOpenSupplyDrop;

            // пытаемся не сделать smash loot
            Timer.Spawn(1000,
                () =>
                {
                    if (supplyCompVis is not null)
                        DirtyFields(supply, supplyCompVis, null,
                            nameof(AirDropVisualizerComponent.SupplyDrop),
                            nameof(AirDropVisualizerComponent.SupplyDropOverride));
                    QueueLocalEvent(new AirDropItemSpawnEvent
                    {
                        DropTable = _prototypeManager.Index(lootTable).Table,
                        SupplyPod = supply,
                        ForceOpenSupplyDrop = forceOpenSupplyDrop
                    });
                });
        }
    }

    private void OnSpawnLoot(AirDropItemSpawnEvent ev)
    {
        if (ev.Handled || TerminatingOrDeleted(ev.SupplyPod))
            return;

        var supplyTransform = Transform(ev.SupplyPod);
        var hasContainer =
            _container.TryGetContainer(ev.SupplyPod, SharedEntityStorageSystem.ContainerName, out var container);

        var openDoorSupplyPod = !hasContainer;

        foreach (var item in _entityTable
                     .GetSpawns(ev.DropTable))
        {
            var entItem = Spawn(item);
            if (!hasContainer)
            {
                _transform.DropNextTo(entItem, ev.SupplyPod);
                continue;
            }

            if (!_container.InsertOrDrop(entItem, container!, supplyTransform))
                openDoorSupplyPod = true;
        }

        if (openDoorSupplyPod || ev.ForceOpenSupplyDrop)
        {
            try
            {
                _storage.OpenStorage(ev.SupplyPod);
            }
            catch (Exception e)
            {
                Log.Warning($"Unable to open {ToPrettyString(ev.SupplyPod)}: {e}");
            }
        }

        ev.Handled = true;
    }
}
