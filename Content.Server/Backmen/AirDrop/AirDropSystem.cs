using Content.Server.Ghost.Roles;
using Content.Server.Ghost.Roles.Components;
using Content.Shared._Lavaland.LimitedUsage;
using Content.Shared.Backmen.AirDrop;
using Content.Shared.EntityTable;
using Content.Shared.Interaction;
using Content.Shared.Storage.EntitySystems;
using Content.Shared.Timing;
using Robust.Shared.Containers;
using Robust.Shared.Map;
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

    private readonly List<(EntityUid Uid, MapCoordinates Pos)> _finishDrop = new();

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
                    _finishDrop.Add((uid, _transform.GetMapCoordinates(uid)));
                    break;
            }
        }

        foreach (var uid in _advanceToDrop)
        {
            if (!TryComp(uid, out AirDropComponent? comp))
                continue;

            comp.Phase = AirDropPhase.Drop;
            comp.PhaseEndTime = curTime + TimeSpan.FromSeconds(comp.TimeToDrop);
            Dirty(uid, comp);
        }

        foreach (var (uid, pos) in _finishDrop)
        {
            if (TerminatingOrDeleted(uid) || !TryComp(uid, out AirDropComponent? comp))
                continue;

            comp.Phase = AirDropPhase.Done;
            comp.PhaseEndTime = TimeSpan.Zero;
            Dirty(uid, comp);

            RaiseLocalEvent(uid,
                new AirDropSpawnEvent
                {
                    Pos = pos
                });
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
        ent.Comp.Phase = AirDropPhase.Target;
        ent.Comp.PhaseEndTime = curTime + TimeSpan.FromSeconds(ent.Comp.TimeOfTarget);
        Dirty(ent);
    }

    private void OnInitialStartup(Entity<AirDropComponent> ent, ref MapInitEvent args)
    {
        // Loot tables spawn entities before inserting into the pod; those must not start another drop.
        if (_container.IsEntityInContainer(ent.Owner))
            return;

        BeginAirDrop(ent);

        if (!TryGetNetEntity(ent, out var airDrop))
            return;

        var coords = _transform.GetMapCoordinates(ent);

        RaiseNetworkEvent(new AirDropStartEvent
            {
                Uid = airDrop.Value,
                Pos = coords
            },
            GetVisualNotifyFilter(ent, coords, ent.Comp.VisualNotifyRange)
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
        Spawn(ent.Comp.AirDropProto, Transform(ent).Coordinates);
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

        var pos = args.Pos;
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
                        Dirty(supply, supplyCompVis);
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
