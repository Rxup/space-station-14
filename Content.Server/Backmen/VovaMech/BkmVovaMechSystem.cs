using Content.Server.Ghost.Roles.Components;
using Content.Server.Popups;
using Content.Shared.ActionBlocker;
using Content.Shared.Backmen.VovaMech;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Components;
using Content.Shared.Vehicle;
using Content.Shared.Vehicle.Components;

namespace Content.Server.Backmen.VovaMech;

public sealed partial class BkmVovaMechSystem : SharedBkmVovaMechSystem
{
    [Dependency] private ActionBlockerSystem _actionBlocker = default!;
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private VehicleSystem _vehicle = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BkmPilotableMechComponent, BkmVovaMechEntryEvent>(OnEntry);
        SubscribeLocalEvent<BkmPilotableMechComponent, VehicleOperatorSetEvent>(OnOperatorSet);
    }

    private void OnEntry(EntityUid uid, BkmPilotableMechComponent component, BkmVovaMechEntryEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        if (!_vehicle.CanOperate(uid, args.User))
        {
            _popup.PopupEntity(Loc.GetString("mech-no-enter", ("item", uid)), args.User);
            return;
        }

        if (!TryInsert(uid, args.User, component))
            return;

        args.Handled = true;
    }

    private void OnOperatorSet(Entity<BkmPilotableMechComponent> ent, ref VehicleOperatorSetEvent args)
    {
        if (args.OldOperator is { } oldOperator)
        {
            RemComp<InteractionRelayComponent>(oldOperator);
            _interaction.SetRelay(oldOperator, null);

            if (ent.Comp.HadGhostTakeover)
            {
                EnsureComp<GhostTakeoverAvailableComponent>(ent);
                ent.Comp.HadGhostTakeover = false;
            }

            _actionBlocker.UpdateCanMove(ent);
        }

        if (args.NewOperator is { } newOperator)
        {
            ent.Comp.HadGhostTakeover = HasComp<GhostTakeoverAvailableComponent>(ent);
            if (ent.Comp.HadGhostTakeover)
                RemComp<GhostTakeoverAvailableComponent>(ent);

            var relay = EnsureComp<InteractionRelayComponent>(newOperator);
            _interaction.SetRelay(newOperator, ent, relay);

            _actionBlocker.UpdateCanMove(ent);
        }
    }
}
