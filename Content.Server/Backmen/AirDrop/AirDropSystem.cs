using Content.Server.Ghost.Roles;
using Content.Server.Ghost.Roles.Components;
using Content.Shared._Lavaland.LimitedUsage;
using Content.Shared.Backmen.AirDrop;
using Content.Shared.EntityTable;
using Content.Shared.Interaction;
using Content.Shared.Storage.EntitySystems;
using Content.Shared.Timing;
using Robust.Shared.Containers;
using Robust.Shared.Timing;

namespace Content.Server.Backmen.AirDrop;

public sealed class AirDropSystem : SharedAirDropSystem
{
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedEntityStorageSystem _storage = default!;
    [Dependency] private readonly EntityTableSystem _entityTable = default!;
    [Dependency] private readonly SharedNoLavalandUsageSystem _noLavaland = default!;


    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<AirDropItemComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<AirDropItemComponent, MapInitEvent>(OnMapInit);

        SubscribeLocalEvent<AirDropComponent, AirDropSpawnEvent>(OnAirDropSpawn);
        SubscribeLocalEvent<AirDropItemSpawnEvent>(OnSpawnLoot);

        SubscribeLocalEvent<AirDropGhostRoleComponent, TakeGhostRoleEvent>(OnTakeOver,
            after: [typeof(GhostRoleSystem)]);
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

        if (Delay.IsDelayed((ent, useDelay)))
            return;

        if (ent.Comp.LavaLandOnly && !_noLavaland.IsApply(ent))
            return;

        Delay.TryResetDelay((ent, useDelay));
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
        Delay.SetLength(dItem, ent.Comp.Cooldown);

        if (ent.Comp.StartCooldown)
        {
            Delay.TryResetDelay((ent, useDelay));
        }
    }

    private async void OnAirDropSpawn(EntityUid uid, AirDropComponent comp, AirDropSpawnEvent args)
    {
        if (args.Handled)
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

        QueueDel(uid);
        args.Handled = true;

        if (comp.SupplyDropTable is { } lootTable)
        {
            var forceOpenSupplyDrop = comp.ForceOpenSupplyDrop;

            // пытаемся не сделать smash loot
            await Timer.Delay(TimeSpan.FromSeconds(1));
            if(supplyCompVis is not null)
                Dirty(supply,supplyCompVis);
            QueueLocalEvent(new AirDropItemSpawnEvent
            {
                DropTable = PrototypeManager.Index(lootTable).Table,
                SupplyPod = supply,
                ForceOpenSupplyDrop = forceOpenSupplyDrop
            });
        }
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
}
