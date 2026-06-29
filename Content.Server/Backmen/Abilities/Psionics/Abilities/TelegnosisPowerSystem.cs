using Content.Server.Atmos.EntitySystems;
using Content.Server.Mind;
using Content.Shared.Actions;
using Content.Shared.Atmos.Components;
using Content.Shared.Backmen.Abilities.Psionics;
using Content.Shared.Backmen.Abilities.Psionics.Events;
using Content.Shared.Backmen.Psionics.Events;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs;
using Content.Shared.StatusEffectNew;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Server.Backmen.Abilities.Psionics;

public sealed partial class TelegnosisPowerSystem : SharedTelegnosisPowerSystem
{
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private MindSystem _mindSystem = default!;
    [Dependency] private SharedPsionicAbilitiesSystem _psionics = default!;
    [Dependency] private MindSwapPowerSystem _mindSwapPowerSystem = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private FlammableSystem _flammableSystem = default!;
    [Dependency] private SharedHandsSystem _handsSystem = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<TelegnosisPowerComponent, MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<TelegnosisPowerComponent, StatusEffectRelayedEvent<MobStateChangedEvent>>(OnMobStateChangedRelayed);

        SubscribeLocalEvent<TelegnosticProjectionComponent, TelegnosisPowerReturnActionEvent>(OnPowerReturnUsed);
        SubscribeLocalEvent<TelegnosticProjectionComponent, MindRemovedMessage>(OnMindRemoved);
        SubscribeLocalEvent<TelegnosticProjectionComponent, MoveEvent>(OnProjectionMove);
        SubscribeLocalEvent<TelegnosticProjectionComponent, DispelledEvent>(OnDispelled);
        SubscribeLocalEvent<TelegnosticProjectionComponent, TelegnosticGetBrainDoAfterEvent>(OnGetBrainsEvent);
    }

    private void OnGetBrainsEvent(Entity<TelegnosticProjectionComponent> ent, ref TelegnosticGetBrainDoAfterEvent args)
    {
        if(args.Cancelled || args.Handled)
            return;

        args.Handled = true;
        var brains = DropAsBain(ent);
        if(brains != null)
            _handsSystem.TryPickupAnyHand(args.User, brains.Value, false);
    }

    private static readonly EntProtoId OrganHumanBrain = "OrganHumanBrain";
    private EntityUid? DropAsBain(Entity<TelegnosticProjectionComponent> ent)
    {
        if(!_mindSystem.TryGetMind(ent, out var mindId, out var mind))
        {
            return null;
        }
        var brains = Spawn(OrganHumanBrain, Transform(ent.Owner).Coordinates);
        _mindSystem.TransferTo(mindId, brains, true, false, mind);
        QueueDel(ent);
        return brains;
    }

    private void OnDispelled(Entity<TelegnosticProjectionComponent> ent, ref DispelledEvent args)
    {
        if (ent.Comp.IsTrapped || TerminatingOrDeleted(ent.Comp.Host))
        {
            DropAsBain(ent);
            return;
        }

        Retract(ent.Comp.Host, ent.Comp.HostComp);

        if (TryComp<FlammableComponent>(ent.Comp.Host, out var flammable))
        {
            _flammableSystem.Ignite(ent.Comp.Host, args.Source ?? EntityUid.Invalid, flammable, args.Source);
            _flammableSystem.SetFireStacks(ent.Comp.Host, 3, flammable, true);
        }
    }

    private void OnProjectionMove(Entity<TelegnosticProjectionComponent> ent, ref MoveEvent args)
    {
        if(ent.Comp.IsTrapped || TerminatingOrDeleted(ent.Comp.Host))
            return;

        CheckTelegnosisMove(ent.Comp.Host, ent.Comp.HostComp, (ent.Owner,  ent.Comp, args.Entity));
    }

    private void EnsureHaveProjection(EntityUid host, TelegnosisPowerComponent hostComp)
    {
        if(!TerminatingOrDeleted(hostComp.TelegnosisProjection))
            return;

        var telegnosis = Spawn(hostComp.Prototype);
        hostComp.TelegnosisProjection = (telegnosis, EnsureComp<TelegnosticProjectionComponent>(telegnosis));

        hostComp.TelegnosisProjection.Comp.Host = host;
        hostComp.TelegnosisProjection.Comp.HostComp = hostComp;

        _container.CleanContainer(hostComp.TelegnosisContainer);
        _container.Insert(telegnosis, hostComp.TelegnosisContainer);
    }

    private static readonly EntProtoId ActionTelegnosis = "ActionTelegnosis";

    protected override void EnsurePowerActions(EntityUid uid, TelegnosisPowerComponent component)
    {
        component.TelegnosisContainer = _container.EnsureContainer<ContainerSlot>(uid, "TelegnosisContainer");
        EnsureHaveProjection(uid, component);

        _actions.AddAction(uid, ref component.TelegnosisPowerAction, ActionTelegnosis);

        if (TryComp<PsionicComponent>(uid, out var psionic) && psionic.PsionicAbility == null)
            psionic.PsionicAbility = component.TelegnosisPowerAction;
    }

    protected override void RemovePowerActions(EntityUid uid, TelegnosisPowerComponent component)
    {
        _actions.RemoveAction(uid, component.TelegnosisPowerAction);
        if(!_mindSystem.TryGetMind(uid, out var mindId, out var mind))
        {
            return;
        }
        _mindSystem.UnVisit(mindId, mind);
        QueueDel(component.TelegnosisProjection);
    }

    private void CheckTelegnosisMove(
        Entity<TransformComponent?> host,
        TelegnosisPowerComponent hostComp,
        Entity<TelegnosticProjectionComponent?, TransformComponent?> telegnosis)
    {
        if (TerminatingOrDeleted(telegnosis) || TerminatingOrDeleted(host))
            return;

        if (!Resolve(host, ref host.Comp) ||
            !Resolve(telegnosis, ref telegnosis.Comp1, ref telegnosis.Comp2))
        {
            return;
        }

        if (!_transform.InRange(telegnosis.Comp2.Coordinates, host.Comp.Coordinates, hostComp.DistanceAllowed))
            Retract(host, hostComp);
    }

    private void Retract(EntityUid host, TelegnosisPowerComponent hostComp)
    {
        DebugTools.Assert(!hostComp.TelegnosisContainer.Contains(hostComp.TelegnosisProjection));
        _container.Insert(hostComp.TelegnosisProjection.Owner, hostComp.TelegnosisContainer, force: true);
        DebugTools.Assert(hostComp.TelegnosisContainer.Contains(hostComp.TelegnosisProjection.Owner));

        if(!_mindSystem.TryGetMind(host, out var mindId, out var mind))
        {
            return;
        }
        _mindSystem.UnVisit(mindId, mind);
    }

    private void TryGotTrapped(EntityUid host, TelegnosisPowerComponent hostComp)
    {
        if (TerminatingOrDeleted(hostComp.TelegnosisProjection))
            return;

        if(hostComp.TelegnosisContainer.Contains(hostComp.TelegnosisProjection.Owner))
            return;

        if (!_mindSystem.TryGetMind(host, out var mindId, out var mind))
            return;

        _mindSwapPowerSystem.GetTrapped(hostComp.TelegnosisProjection);
        _mindSystem.UnVisit(mindId, mind);
        _mindSystem.TransferTo(mindId, hostComp.TelegnosisProjection, true, false, mind);
    }

    private void OnMobStateChangedRelayed(Entity<TelegnosisPowerComponent> ent, ref StatusEffectRelayedEvent<MobStateChangedEvent> args)
    {
        if (args.Args.NewMobState != MobState.Dead)
            return;

        TryGotTrapped(args.Args.Target, ent.Comp);
    }

    private void OnMobStateChanged(EntityUid uid, TelegnosisPowerComponent component, MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead)
            return;

        TryGotTrapped(uid, component);
    }

    private void OnPowerReturnUsed(EntityUid uid, TelegnosticProjectionComponent component, TelegnosisPowerReturnActionEvent args)
    {
        if (
            !TryComp<VisitingMindComponent>(args.Performer, out var mindId) ||
            mindId!.MindId == null ||
            !TryComp<MindComponent>(mindId.MindId.Value, out var mind)
            )
            return;

        _mindSystem.UnVisit(mindId.MindId.Value, mind);
        _container.Insert(args.Performer, component.HostComp.TelegnosisContainer, force: true);
        _psionics.LogPowerUsed(uid, "telegnosis");
        args.Handled = true;
    }

    protected override void HandlePowerUse(EntityUid uid, TelegnosisPowerComponent component, TelegnosisPowerActionEvent args)
    {
        if (args.Handled)
            return;

        if (!_mindSystem.TryGetMind(args.Performer, out var mindId, out var mind))
            return;

        EnsureHaveProjection(args.Performer, component);

        _mindSystem.Visit(mindId, component.TelegnosisProjection, mind);

        _container.Remove(
            component.TelegnosisProjection.Owner,
            component.TelegnosisContainer,
            true,
            true,
            Transform(args.Performer).Coordinates);

        _psionics.LogPowerUsed(args.Performer, "telegnosis");
        args.Handled = true;
    }
    private void OnMindRemoved(EntityUid uid, TelegnosticProjectionComponent component, MindRemovedMessage args)
    {
        _container.Insert(uid, component.HostComp.TelegnosisContainer, force: true);
    }
}


