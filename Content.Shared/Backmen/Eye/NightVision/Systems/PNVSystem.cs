using Content.Shared.Backmen.Eye.NightVision.Components;
using Content.Shared.Inventory;
using Content.Shared.Actions;
using Content.Shared.Inventory.Events;

namespace Content.Shared.Backmen.Eye.NightVision.Systems;

public sealed class PNVSystem : EntitySystem
{
    [Dependency] private readonly NightVisionSystem _nightvisionableSystem = default!;
    [Dependency] private readonly IEntityManager _entManager = default!;
    [Dependency] private readonly SharedActionsSystem _actionsSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PNVComponent, GotEquippedEvent>(OnEquipped);
        SubscribeLocalEvent<PNVComponent, GotUnequippedEvent>(OnUnequipped);
        SubscribeLocalEvent<PNVComponent, InventoryRelayedEvent<CanVisionAttemptEvent>>(OnPNVTrySee);
    }

    private void OnPNVTrySee(EntityUid uid, PNVComponent component, InventoryRelayedEvent<CanVisionAttemptEvent> args)
    {
        args.Args.Cancel();
    }

    private void OnEquipped(EntityUid uid, PNVComponent component, GotEquippedEvent args)
    {
        var nvcomp = _entManager.GetComponent<NightVisionComponent>(args.Equipee);
        if (nvcomp == null)
            return;

        _nightvisionableSystem.UpdateIsNightVision(args.Equipee);
        _actionsSystem.AddAction(args.Equipee, ref component.ActionContainer, component.ActionProto);
    }

    private void OnUnequipped(EntityUid uid, PNVComponent component, GotUnequippedEvent args)
    {
	    var nvcomp = _entManager.GetComponent<NightVisionComponent>(args.Equipee);
        if (nvcomp == null)
            return;

        _nightvisionableSystem.UpdateIsNightVision(args.Equipee);
        _actionsSystem.RemoveAction(args.Equipee, component.ActionContainer);
    }
}
