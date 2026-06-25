using Content.Shared.Body;
using Content.Shared.Backmen.Body.Systems; // backmen: body
using Content.Shared.Gibbing;
using Content.Shared.Inventory;
using Content.Shared.Trigger.Components.Effects;
using Robust.Shared.Network;

namespace Content.Shared.Trigger.Systems;

public sealed partial class GibOnTriggerSystem : XOnTriggerSystem<GibOnTriggerComponent>
{
    [Dependency] private BkmBodySharedSystem _body = default!; // backmen: body
    [Dependency] private GibbingSystem _gibbing = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private INetManager _net = default!;

    protected override void OnTrigger(Entity<GibOnTriggerComponent> ent, EntityUid target, ref TriggerEvent args)
    {
        if (ent.Comp.DeleteItems)
        {
            var items = _inventory.GetHandOrInventoryEntities(target);
            foreach (var item in items)
            {
                PredictedQueueDel(item);
            }
        }

        if (_net.IsServer)
        {
            if (TryComp<BodyComponent>(target, out var body))
                _body.GibBody(target, gibOrgans: true, body);
            else
                _gibbing.Gib(target, user: args.User);
        }
        args.Handled = true;
    }
}
