using Content.Shared.Interaction.Events;
using Content.Shared.Backmen.AccessGunBlockerSystem;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.GameStates;

namespace Content.Client.Backmen.AccessWeaponBlockerSystem;

public sealed class AccessWeaponBlockerSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AccessWeaponBlockerComponent, AttemptShootEvent>(OnShootAttempt);
        SubscribeLocalEvent<AccessWeaponBlockerComponent, AttemptMeleeEvent>(OnMeleeAttempt);
        SubscribeLocalEvent<AccessWeaponBlockerComponent, UseAttemptEvent>(OnUseAttempt);
        SubscribeLocalEvent<AccessWeaponBlockerComponent, InteractionAttemptEvent>(OnInteractAttempt);
        SubscribeLocalEvent<AccessWeaponBlockerComponent, ComponentHandleState>(OnFactionWeaponBlockerHandleState);
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

    private void OnFactionWeaponBlockerHandleState(EntityUid uid, AccessWeaponBlockerComponent component, ref ComponentHandleState args)
    {
        if (args.Current is not AccessWeaponBlockerComponentState state)
            return;

        component.CanUse = state.CanUse;
        component.AlertText = state.AlertText;
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
