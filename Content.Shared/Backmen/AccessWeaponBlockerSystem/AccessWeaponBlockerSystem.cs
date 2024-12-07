using Content.Shared.Access.Components;
using Content.Shared.Hands;
using Content.Shared.Interaction.Events;
using Content.Shared.Inventory;
using Content.Shared.PDA;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Weapons.Ranged.Systems;

namespace Content.Shared.Backmen.AccessWeaponBlockerSystem;

public sealed class AccessWeaponBlockerSystem : EntitySystem
{
    [Dependency] private readonly InventorySystem _inventorySystem = default!;
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AccessWeaponBlockerComponent, AttemptShootEvent>(OnShootAttempt);
        SubscribeLocalEvent<AccessWeaponBlockerComponent, AttemptMeleeEvent>(OnMeleeAttempt);
        SubscribeLocalEvent<AccessWeaponBlockerComponent, UseAttemptEvent>(OnUseAttempt);
        SubscribeLocalEvent<AccessWeaponBlockerComponent, InteractionAttemptEvent>(OnInteractAttempt);
        SubscribeLocalEvent<AccessWeaponBlockerComponent, GotEquippedHandEvent>(OnGotEquippedHand);
    }

    private void OnUseAttempt(EntityUid uid, AccessWeaponBlockerComponent component, ref UseAttemptEvent args)
    {
        if (component.CanUse)
            return;

        args.Cancel();
    }

    private void OnInteractAttempt(EntityUid uid, AccessWeaponBlockerComponent component, ref InteractionAttemptEvent args)
    {
        if (component.CanUse)
            return;

        args.Cancelled = true;
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

    private void OnGotEquippedHand(EntityUid uid, AccessWeaponBlockerComponent component, ref GotEquippedHandEvent args)
    {
        if (!_inventorySystem.TryGetSlotEntity(args.User, "id", out var slotCardUid))
            return;
        var accessEntity = TryComp<PdaComponent>(slotCardUid, out var pda) && pda.ContainedId is { } pdaSlot
            ? pdaSlot
            : slotCardUid.Value;
        component.CanUse = IsAnyAccess(accessEntity, component);
        Dirty(uid, component);
    }

    private bool IsAnyAccess(EntityUid accessEntity, AccessWeaponBlockerComponent component)
    {
        if (!TryComp<AccessComponent>(accessEntity, out var access))
            return false;
        foreach (var accessTag in access.Tags)
        {
            if (component.Access.Contains(accessTag))
                return true;
        }
        return false;
    }
}
