using Content.Shared.Hands;
using Content.Shared.Access.Components;
using Content.Shared.Backmen.AccessGunBlockerSystem;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.GameStates;
using Content.Shared.Inventory;
using Content.Shared.PDA;

namespace Content.Server.Backmen.AccessWeaponBlockerSystem;

public sealed class AccessWeaponBlockerSystem : EntitySystem
{
    [Dependency] private readonly InventorySystem _inventorySystem = default!;
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AccessWeaponBlockerComponent, AttemptShootEvent>(OnShootAttempt);
        SubscribeLocalEvent<AccessWeaponBlockerComponent, AttemptMeleeEvent>(OnMeleeAttempt);
        SubscribeLocalEvent<AccessWeaponBlockerComponent, ComponentGetState>(OnGetState);
        SubscribeLocalEvent<AccessWeaponBlockerComponent, GotEquippedHandEvent>(OnGotEquippedHand);
    }


    private void OnGotEquippedHand(Entity<AccessWeaponBlockerComponent> accessBlocker, ref GotEquippedHandEvent args)
    {
        if (!_inventorySystem.TryGetSlotEntity(args.User, "id", out var slotCardUid))
            return;
        var accessEntity = TryComp<PdaComponent>(slotCardUid, out var pda) && pda.ContainedId is { } pdaSlot
            ? pdaSlot
            : slotCardUid.Value;
        accessBlocker.Comp.CanUse = IsAnyAccess(accessEntity, accessBlocker);
        Dirty(accessBlocker);
    }

    private bool IsAnyAccess(EntityUid accessEntity, Entity<AccessWeaponBlockerComponent> accessBlocker)
    {
        if (!TryComp<AccessComponent>(accessEntity, out var access))
            return false;
        foreach (var accessTag in access.Tags)
        {
            if (accessBlocker.Comp.Access.Contains(accessTag))
                return true;
        }
        return false;
    }
    private void OnGetState(EntityUid uid, AccessWeaponBlockerComponent component, ref ComponentGetState args)
    {
        args.State = new AccessWeaponBlockerComponentState()
        {
            CanUse = component.CanUse,
            AlertText = component.AlertText
        };
    }

    private void OnMeleeAttempt(EntityUid uid, AccessWeaponBlockerComponent component, ref AttemptMeleeEvent args)
    {
        if (component.CanUse)
            return;

        args.Cancelled = true;
        args.Message = component.AlertText;
    }

    private void OnShootAttempt(EntityUid uid, AccessWeaponBlockerComponent component, ref AttemptShootEvent args)
    {
        if (component.CanUse)
            return;

        args.Cancelled = true;
        args.Message = component.AlertText;
    }
}
