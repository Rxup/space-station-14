using Content.Shared.Backmen.Eye.NightVision.Components;
using Content.Shared.Inventory;
using Content.Shared.Actions;
using Content.Shared.Inventory.Events;

namespace Content.Shared.Backmen.Eye.NightVision.Systems;

public sealed class PNVSystem : EntitySystem
{
    [Dependency] private readonly NightVisionSystem _nightvisionableSystem = default!;
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
        if (args.Slot is not ("eyes" or "mask" or "head"))
            return;

        if (!TryComp<NightVisionComponent>(args.Equipee, out var nvcomp))
            return;

        _nightvisionableSystem.UpdateIsNightVision(args.Equipee, nvcomp);
        _actionsSystem.AddAction(args.Equipee, ref component.ActionContainer, component.ActionProto);
        _actionsSystem.SetCooldown(component.ActionContainer, TimeSpan.FromSeconds(1)); // GCD?
    }

    private void OnUnequipped(EntityUid uid, PNVComponent component, GotUnequippedEvent args)
    {
        if (args.Slot is not ("eyes" or "mask" or "head"))
            return;

        if (!TryComp<NightVisionComponent>(args.Equipee, out var nvcomp))
            return;

        _nightvisionableSystem.UpdateIsNightVision(args.Equipee, nvcomp);
        _actionsSystem.RemoveAction(args.Equipee, component.ActionContainer);
    }
}
