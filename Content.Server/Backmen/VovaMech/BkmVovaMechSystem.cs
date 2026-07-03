using Content.Server.Ghost.Roles.Components;
using Content.Server.Popups;
using Content.Shared.ActionBlocker;
using Content.Shared.Backmen.VovaMech;
using Content.Shared.DoAfter;
using Content.Shared.IdentityManagement;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Components;
using Content.Shared.Mind.Components;
using Content.Shared.Popups;
using Content.Shared.Vehicle;
using Content.Shared.Vehicle.Components;
using Content.Shared.Verbs;

namespace Content.Server.Backmen.VovaMech;

public sealed partial class BkmVovaMechSystem : SharedBkmVovaMechSystem
{
    [Dependency] private ActionBlockerSystem _actionBlocker = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private VehicleSystem _vehicle = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BkmPilotableMechComponent, GetVerbsEvent<AlternativeVerb>>(OnAlternativeVerb);
        SubscribeLocalEvent<BkmPilotableMechComponent, GetVerbsEvent<InteractionVerb>>(OnInteractionVerb);
        SubscribeLocalEvent<BkmPilotableMechComponent, BkmVovaMechEntryEvent>(OnEntry);
        SubscribeLocalEvent<BkmPilotableMechComponent, BkmVovaMechExitEvent>(OnExit);
        SubscribeLocalEvent<BkmPilotableMechComponent, VehicleOperatorSetEvent>(OnOperatorSet);
    }

    private void OnAlternativeVerb(EntityUid uid, BkmPilotableMechComponent component, GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        if (CanAttemptEntry(uid, args.User, component))
        {
            args.Verbs.Add(new AlternativeVerb
            {
                Text = Loc.GetString("mech-verb-enter"),
                Category = VerbCategory.Insert,
                Act = () => StartEntryDoAfter(uid, component, args.User),
            });
        }
        else if (_vehicle.HasOperator(uid))
        {
            args.Verbs.Add(CreateEjectVerb(uid, component, args.User, () => new AlternativeVerb()));
        }
    }

    private void OnInteractionVerb(EntityUid uid, BkmPilotableMechComponent component, GetVerbsEvent<InteractionVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        if (CanAttemptEntry(uid, args.User, component))
        {
            args.Verbs.Add(new InteractionVerb
            {
                Text = Loc.GetString("mech-verb-enter"),
                Category = VerbCategory.Insert,
                Act = () => StartEntryDoAfter(uid, component, args.User),
            });
        }
        else if (_vehicle.HasOperator(uid))
        {
            args.Verbs.Add(CreateEjectVerb(uid, component, args.User, () => new InteractionVerb()));
        }
    }

    private T CreateEjectVerb<T>(EntityUid uid, BkmPilotableMechComponent component, EntityUid user, Func<T> createVerb)
        where T : Verb, new()
    {
        var operatorUid = _vehicle.GetOperatorOrNull(uid);
        var ejectVerb = createVerb();
        ejectVerb.Text = Loc.GetString("mech-verb-exit");
        ejectVerb.Category = VerbCategory.Eject;
        ejectVerb.Priority = 1;
        ejectVerb.Act = () =>
        {
            if (user == operatorUid)
            {
                TryEject(uid, component);
                return;
            }

            var doAfterEventArgs = new DoAfterArgs(EntityManager, user, component.ExitDelay, new BkmVovaMechExitEvent(), uid, target: uid)
            {
                BreakOnMove = true,
            };
            _popup.PopupEntity(
                Loc.GetString("mech-eject-pilot-alert", ("item", uid), ("user", Identity.Entity(user, EntityManager))),
                uid,
                PopupType.Large);

            _doAfter.TryStartDoAfter(doAfterEventArgs);
        };
        return ejectVerb;
    }

    private void StartEntryDoAfter(EntityUid uid, BkmPilotableMechComponent component, EntityUid user)
    {
        var doAfterEventArgs = new DoAfterArgs(EntityManager, user, component.EntryDelay, new BkmVovaMechEntryEvent(), uid, target: uid)
        {
            BreakOnMove = true,
        };

        _doAfter.TryStartDoAfter(doAfterEventArgs);
    }

    private bool CanAttemptEntry(EntityUid uid, EntityUid user, BkmPilotableMechComponent component)
    {
        if (!CanInsert(uid, user, component))
            return false;

        if (TryComp<MindContainerComponent>(uid, out var mind) && mind.HasMind)
            return false;

        return _vehicle.CanOperate(uid, user);
    }

    private void OnEntry(EntityUid uid, BkmPilotableMechComponent component, BkmVovaMechEntryEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        if (TryComp<MindContainerComponent>(uid, out var mind) && mind.HasMind)
        {
            _popup.PopupEntity(Loc.GetString("mech-no-enter", ("item", uid)), args.User);
            return;
        }

        if (!_vehicle.CanOperate(uid, args.User))
        {
            _popup.PopupEntity(Loc.GetString("mech-no-enter", ("item", uid)), args.User);
            return;
        }

        if (!TryInsert(uid, args.User, component))
            return;

        args.Handled = true;
    }

    private void OnExit(EntityUid uid, BkmPilotableMechComponent component, BkmVovaMechExitEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        if (!TryEject(uid, component))
            return;

        args.Handled = true;
    }

    private void OnOperatorSet(Entity<BkmPilotableMechComponent> ent, ref VehicleOperatorSetEvent args)
    {
        if (args.OldOperator is { } oldOperator)
        {
            // InteractionRelayComponent is already removed by VehicleSystem.TrySetOperator (RemCompDeferred).

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

            EnsureActiveMechHand(ent.Owner); // backmen: vova-mech-hands-ui

            _actionBlocker.UpdateCanMove(ent);
        }
    }
}
