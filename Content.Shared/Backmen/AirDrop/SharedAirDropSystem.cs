using Content.Shared.EntityTable;
using Content.Shared.Interaction;
using Content.Shared.Storage.EntitySystems;
using Content.Shared.Timing;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared.Backmen.AirDrop;

public abstract class SharedAirDropSystem : EntitySystem
{
    [Dependency] private readonly UseDelaySystem _delay = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly EntityTableSystem _entityTable = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedEntityStorageSystem _storage = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AirDropItemComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<AirDropComponent, MapInitEvent>(OnInitialStartup);
        SubscribeLocalEvent<AirDropComponent, AirDropSpawnEvent>(OnAirDropSpawn);
        SubscribeLocalEvent<AirDropComponent, AirDropTargetSpawnEvent>(OnAirDropTargetSpawn);
        SubscribeLocalEvent<AirDropItemComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<AirDropItemSpawnEvent>(OnSpawnLoot);
        SubscribeNetworkEvent<AirDropStartEvent>(OnStartAirDrop);
    }

    private void OnStartAirDrop(AirDropStartEvent ev)
    {
        if (_net.IsServer || !TryGetEntity(ev.Uid, out var supplyPod))
            return;
        StartAirDrop(supplyPod.Value, ev.Pos);
    }

    private void OnSpawnLoot(AirDropItemSpawnEvent ev)
    {
        if (ev.Handled)
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
            _storage.OpenStorage(ev.SupplyPod);
        }

        ev.Handled = true;
    }

    private void OnInitialStartup(Entity<AirDropComponent> ent, ref MapInitEvent args)
    {
        var pos = _transform.GetMapCoordinates(ent);
        StartAirDrop((ent, ent), pos);
        if (_net.IsServer && TryGetNetEntity(ent, out var airDrop))
        {
            RaiseNetworkEvent(new AirDropStartEvent
                {
                    Uid = airDrop.Value,
                    Pos = pos
                },
                Filter.Pvs(ent)
            );
        }
    }

    private void StartAirDrop(Entity<AirDropComponent?> ent, MapCoordinates pos)
    {
        if (!Resolve(ent, ref ent.Comp))
            return;

        var marker = EntityUid.Invalid;

        if (_net.IsClient)
        {
            marker = Spawn(ent.Comp.DropTargetProto, pos, ent.Comp.DropTarget);
        }

        Timer.Spawn(TimeSpan.FromSeconds(ent.Comp.TimeOfTarget),
            () =>
            {
                if (!TerminatingOrDeleted(ent))
                {
                    RaiseLocalEvent(ent,
                        new AirDropTargetSpawnEvent
                        {
                            Pos = pos
                        });
                }

                if (_net.IsClient)
                    Del(marker);
            });
    }

    private void OnAirDropTargetSpawn(Entity<AirDropComponent> ent, ref AirDropTargetSpawnEvent args)
    {
        if (args.Handled)
            return;

        var pos = args.Pos;
        var marker = EntityUid.Invalid;
        if (_net.IsClient)
        {
            marker = Spawn(ent.Comp.InAirProto, pos, ent.Comp.InAir);
            _transform.AttachToGridOrMap(marker);
        }

        Timer.Spawn(TimeSpan.FromSeconds(ent.Comp.TimeToDrop),
            () =>
            {
                if (!TerminatingOrDeleted(ent))
                {
                    RaiseLocalEvent(ent,
                        new AirDropSpawnEvent
                        {
                            Pos = pos
                        });
                }

                if (_net.IsClient)
                    Del(marker);
            });

        args.Handled = true;
    }

    private async void OnAirDropSpawn(EntityUid uid, AirDropComponent comp, AirDropSpawnEvent args)
    {
        if (args.Handled)
            return;

        if (!_net.IsServer)
            return;

        var pos = args.Pos;
        var supply = Spawn(comp.SupplyDropProto, pos, comp.SupplyDrop);
        _transform.AttachToGridOrMap(supply);
        if (Prototype(uid) is { } proto)
        {
            EnsureComp<AirDropVisualizerComponent>(supply).SupplyDrop = proto.ID;
            if (TryComp<AirDropGhostRoleComponent>(supply, out var dropGhost))
            {
                dropGhost.SupplyDrop = proto.ID;
            }
        }

        QueueDel(uid);
        args.Handled = true;

        if (comp.SupplyDropTable is { } lootTable)
        {
            var forceOpenSupplyDrop = comp.ForceOpenSupplyDrop;

            // пытаемся не сделать smash loot
            await Timer.Delay(TimeSpan.FromSeconds(1));
            QueueLocalEvent(new AirDropItemSpawnEvent
            {
                DropTable = _prototypeManager.Index(lootTable).Table,
                SupplyPod = supply,
                ForceOpenSupplyDrop = forceOpenSupplyDrop
            });
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

        _delay.TryResetDelay((ent, useDelay));
        Spawn(ent.Comp.AirDropProto, Transform(ent).Coordinates);
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
}
