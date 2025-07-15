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
    [Dependency] protected readonly UseDelaySystem Delay = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] protected readonly IPrototypeManager PrototypeManager = default!;
    [Dependency] protected readonly SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AirDropComponent, MapInitEvent>(OnInitialStartup);
        SubscribeLocalEvent<AirDropComponent, AirDropTargetSpawnEvent>(OnAirDropTargetSpawn);
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

    protected void StartAirDrop(Entity<AirDropComponent?> ent, MapCoordinates pos)
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
}
