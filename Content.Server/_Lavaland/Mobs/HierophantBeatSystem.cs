using Content.Shared._Lavaland.Mobs.Components;
using Content.Shared.Alert;
using Content.Shared.Movement.Systems;
using Content.Shared._vg.TileMovement;
using Content.Shared.Movement.Components;

namespace Content.Server._Lavaland.Mobs;

public sealed class HierophantBeatSystem : EntitySystem
{
    [Dependency] private readonly AlertsSystem _alertsSystem = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _speed = default!;
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HierophantBeatComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<HierophantBeatComponent, ComponentRemove>(OnRemove);
        SubscribeLocalEvent<HierophantBeatComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshSpeed);
    }

    private void OnStartup(EntityUid uid, HierophantBeatComponent component, ref ComponentStartup args)
    {
        _alertsSystem.ShowAlert(uid, component.HierophantBeatAlertKey);
        EnsureComp<TileMovementComponent>(uid);
        if (TryComp<MovementSpeedModifierComponent>(uid, out var speedModifier))
            _speed.RefreshMovementSpeedModifiers(uid, speedModifier);
    }

    private void OnRemove(EntityUid uid, HierophantBeatComponent component, ref ComponentRemove args)
    {
        if (TerminatingOrDeleted(uid))
            return;

        _alertsSystem.ClearAlert(uid, component.HierophantBeatAlertKey);
        if (HasComp<TileMovementComponent>(uid))
            RemComp<TileMovementComponent>(uid);
    }

    private void OnRefreshSpeed(EntityUid uid, HierophantBeatComponent component, ref RefreshMovementSpeedModifiersEvent args)
    {
        args.ModifySpeed(component.MovementSpeedBuff, component.MovementSpeedBuff);
    }
}
