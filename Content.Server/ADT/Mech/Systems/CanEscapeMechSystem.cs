using Content.Server.Popups;
using Content.Shared.ActionBlocker;
using Content.Shared.DoAfter;
using Content.Shared.Movement.Events;
using Content.Shared.Resist;
using Robust.Shared.Containers;
using Content.Server.ADT.Mech.Components;
using Content.Server.Mech.Equipment.Components;

namespace Content.Server.Resist;

public sealed class EscapeMechSystem : EntitySystem // Копия существующей системы с изменениями под клешни мехов
{
    [Dependency] private readonly SharedDoAfterSystem _doAfterSystem = default!;
    [Dependency] private readonly PopupSystem _popupSystem = default!;
    [Dependency] private readonly SharedContainerSystem _containerSystem = default!;
    [Dependency] private readonly ActionBlockerSystem _actionBlockerSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CanEscapeMechComponent, MoveInputEvent>(OnRelayMovement);
        SubscribeLocalEvent<CanEscapeMechComponent, EscapeInventoryEvent>(OnEscape);
        SubscribeLocalEvent<CanEscapeMechComponent, EntParentChangedMessage>(OnDropped);
    }

    private void OnRelayMovement(EntityUid uid, CanEscapeMechComponent component, ref MoveInputEvent args)
    {
        if (!args.HasDirectionalMovement)
            return;

        if (!_containerSystem.TryGetContainingContainer((uid, null, null), out var container) || !_actionBlockerSystem.CanInteract(uid, container.Owner))
            return;

        // Make sure there's nothing stopped the removal (like being glued)
        if (!_containerSystem.CanRemove(uid, container))
        {
            _popupSystem.PopupEntity(Loc.GetString("escape-inventory-component-failed-resisting"), uid, uid);
            return;
        }

        // Uncontested
        if (HasComp<MechGrabberComponent>(container.Owner))
            AttemptEscape(uid, container.Owner, component);
    }

    private void AttemptEscape(EntityUid user, EntityUid container, CanEscapeMechComponent component, float multiplier = 1f)
    {
        if (component.IsEscaping)
            return;
        if (!TryComp<MechGrabberComponent>(container, out var grabber))
            return;

        var doAfterEventArgs = new DoAfterArgs(EntityManager, user, grabber.BaseResistTime * multiplier, new EscapeInventoryEvent(), user, target: Transform(container).ParentUid)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = false
        };

        if (!_doAfterSystem.TryStartDoAfter(doAfterEventArgs, out component.DoAfter))
            return;

        _popupSystem.PopupEntity(Loc.GetString("escape-inventory-component-start-resisting"), user, user);
        _popupSystem.PopupEntity(Loc.GetString("escape-inventory-component-start-resisting-target"), container, container);
    }

    private void OnEscape(EntityUid uid, CanEscapeMechComponent component, EscapeInventoryEvent args)
    {
        component.DoAfter = null;

        if (args.Handled || args.Cancelled)
            return;

        if (!args.Target.HasValue)
            return;

        _containerSystem.TryRemoveFromContainer(uid, true);
        _containerSystem.TryRemoveFromContainer(uid, true); // Второй раз чтобы сущность не оказалась внутри меха

        args.Handled = true;
    }

    private void OnDropped(EntityUid uid, CanEscapeMechComponent component, ref EntParentChangedMessage args)
    {
        if (component.DoAfter != null)
            _doAfterSystem.Cancel(component.DoAfter);
    }
}
