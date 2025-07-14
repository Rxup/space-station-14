using Content.Server.Ghost.Roles;
using Content.Server.Ghost.Roles.Components;
using Content.Shared.Backmen.AirDrop;

namespace Content.Server.Backmen.AirDrop;

public sealed class AirDropSystem : SharedAirDropSystem
{
    public override void Initialize()
    {
        base.Initialize();
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
}
