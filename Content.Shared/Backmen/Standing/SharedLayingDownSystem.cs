using Content.Shared.Backmen.CCVar;
using Content.Shared.Body.Components;
using Content.Shared.Buckle;
using Content.Shared.Buckle.Components;
using Content.Shared.Damage;
using Content.Shared.DoAfter;
using Content.Shared.Gravity;
using Content.Shared.Input;
using Content.Shared.Interaction;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared.Standing;
using Content.Shared.Stunnable;
using Content.Shared.Tag;
using Content.Shared.Traits.Assorted;
using Content.Shared.UserInterface;
using Robust.Shared.Configuration;
using Robust.Shared.Containers;
using Robust.Shared.Input.Binding;
using Robust.Shared.Map.Components;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Serialization;

namespace Content.Shared.Backmen.Standing;

public abstract class SharedLayingDownSystem : EntitySystem
{
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly StandingStateSystem _standing = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedGravitySystem _gravity = default!;
    [Dependency] private readonly ISharedPlayerManager _playerManager = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedBuckleSystem _buckle = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly PullingSystem _pulling = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly TagSystem _tag = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;

    [Dependency] private readonly IConfigurationManager _config = default!;

    protected bool CrawlUnderTables = false;

    public override void Initialize()
    {
        CommandBinds.Builder
            .Bind(ContentKeyFunctions.ToggleStanding, InputCmdHandler.FromDelegate(ToggleStanding))
            .Register<SharedLayingDownSystem>();

        SubscribeAllEvent<ChangeLayingDownEvent>(OnChangeState);

        SubscribeLocalEvent<StandingStateComponent, StandingUpDoAfterEvent>(OnStandingUpDoAfter);
        SubscribeLocalEvent<LayingDownComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshMovementSpeed);
        SubscribeLocalEvent<LayingDownComponent, EntParentChangedMessage>(OnParentChanged);
        SubscribeLocalEvent<LayingDownComponent, MobStateChangedEvent>(OnChangeMobState);

        SubscribeLocalEvent<LayingDownComponent, BuckledEvent>(OnBuckled);
        SubscribeLocalEvent<LayingDownComponent, UnbuckledEvent>(OnUnBuckled);
        SubscribeLocalEvent<LayingDownComponent, StandAttemptEvent>(OnCheckLegs);
        SubscribeLocalEvent<BoundUserInterfaceMessageAttempt>(OnBoundUserInterface, after: [typeof(SharedInteractionSystem)]);

        Subs.CVar(_config, CCVars.CrawlUnderTables, b => CrawlUnderTables = b, true);
    }

    private void OnCheckLegs(Entity<LayingDownComponent> ent, ref StandAttemptEvent args)
    {
        if(!TryComp<BodyComponent>(ent, out var body))
            return;

        if (body.LegEntities.Count < body.RequiredLegs || body.LegEntities.Count == 0)
            args.Cancel(); // no legs bro
    }

    private void OnBoundUserInterface(BoundUserInterfaceMessageAttempt args)
    {
        if(
            args.Cancelled ||
            !TryComp<ActivatableUIComponent>(args.Target, out var uiComp) ||
            !TryComp<StandingStateComponent>(args.Actor, out var standingStateComponent) ||
            standingStateComponent.CurrentState != StandingState.Lying)
            return;

        if(uiComp.RequiresComplex)
            args.Cancel();
    }

    private void OnChangeMobState(Entity<LayingDownComponent> ent, ref MobStateChangedEvent args)
    {
        if(
            !TryComp<StandingStateComponent>(ent, out var standingStateComponent) ||
            standingStateComponent.CurrentState != StandingState.Lying)
            return;

        if (args.NewMobState == MobState.Alive)
        {
            AutoGetUp(ent);
            TryStandUp(ent, ent, standingStateComponent);
            return;
        }

        if (CrawlUnderTables)
        {
            ent.Comp.DrawDowned = false;
            Dirty(ent,ent.Comp);
        }
    }



    private void OnUnBuckled(Entity<LayingDownComponent> ent, ref UnbuckledEvent args)
    {
        if(!TryComp<StandingStateComponent>(ent, out var standingStateComponent))
            return;

        if (TryComp<BodyComponent>(ent, out var body) &&
            ((body.RequiredLegs > 0 && body.LegEntities.Count < body.RequiredLegs) || body.LegEntities.Count == 0)
            && standingStateComponent.CurrentState != StandingState.Lying)
        {
            _standing.Down(ent, true, true, true);
            return;
        }

        TryProcessAutoGetUp(ent);

        if (CrawlUnderTables && standingStateComponent.CurrentState == StandingState.Lying)
        {
            ent.Comp.DrawDowned = true;
            Dirty(ent,ent.Comp);
        }
    }

    private void OnBuckled(Entity<LayingDownComponent> ent, ref BuckledEvent args)
    {
        if(
            !TryComp<StandingStateComponent>(ent, out var standingStateComponent) ||
            standingStateComponent.CurrentState != StandingState.Lying)
            return;

        if (CrawlUnderTables)
        {
            ent.Comp.DrawDowned = false;
            Dirty(ent, ent.Comp);
        }
    }

    protected abstract bool GetAutoGetUp(Entity<LayingDownComponent> ent, ICommonSession session);

    public void TryProcessAutoGetUp(Entity<LayingDownComponent> ent)
    {
        if(_buckle.IsBuckled(ent))
            return;

        if(_pulling.IsPulled(ent))
            return;

        var autoUp = !_playerManager.TryGetSessionByEntity(ent, out var player) ||
                     GetAutoGetUp(ent, session: player);

        if (autoUp && !_container.IsEntityInContainer(ent))
            TryStandUp(ent, ent);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        CommandBinds.Unregister<SharedLayingDownSystem>();
    }

    private void ToggleStanding(ICommonSession? session)
    {
        if (session?.AttachedEntity == null ||
            !HasComp<LayingDownComponent>(session.AttachedEntity) ||
            _gravity.IsWeightless(session.AttachedEntity.Value))
        {
            return;
        }

        if (_net.IsServer)
        {
            RaiseNetworkEvent(new ChangeLayingDownEvent(), Filter.Pvs(session.AttachedEntity.Value));
        }
        else
        {
            RaiseNetworkEvent(new ChangeLayingDownEvent());
        }

    }

    public virtual void AutoGetUp(Entity<LayingDownComponent> ent)
    {

    }

    private void OnChangeState(ChangeLayingDownEvent ev, EntitySessionEventArgs args)
    {
        if (!args.SenderSession.AttachedEntity.HasValue)
            return;

        var uid = args.SenderSession.AttachedEntity.Value;

        // TODO: Wizard
        //if (HasComp<FrozenComponent>(uid))
        //   return;

        if (!TryComp(uid, out StandingStateComponent? standing) ||
            !TryComp(uid, out LayingDownComponent? layingDown) ||
            !TryComp<InputMoverComponent>(uid, out var inputMover))
        {
            return;
        }

        if (
            HasComp<KnockedDownComponent>(uid) ||
            !_mobState.IsAlive(uid) ||
            !inputMover.CanMove)
            return;

        //RaiseNetworkEvent(new CheckAutoGetUpEvent(GetNetEntity(uid)));
        AutoGetUp((uid,layingDown));

        if (_standing.IsDown(uid, standing))
            TryStandUp(uid, layingDown, standing);
        else
            TryLieDown(uid, layingDown, standing);
    }

    private void OnStandingUpDoAfter(EntityUid uid, StandingStateComponent component, StandingUpDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled || HasComp<KnockedDownComponent>(uid) ||
            _mobState.IsIncapacitated(uid) || !_standing.Stand(uid))
        {
            component.CurrentState = StandingState.Lying;
        }

        component.CurrentState = StandingState.Standing;
        Dirty(uid,component);
    }

    private void OnRefreshMovementSpeed(EntityUid uid, LayingDownComponent component, RefreshMovementSpeedModifiersEvent args)
    {
        if (_standing.IsDown(uid))
            args.ModifySpeed(component.SpeedModify, component.SpeedModify);
        //else
          //  args.ModifySpeed(1f, 1f);
    }

    private void OnParentChanged(EntityUid uid, LayingDownComponent component, EntParentChangedMessage args)
    {
        // If the entity is not on a grid, try to make it stand up to avoid issues
        if (!TryComp<StandingStateComponent>(uid, out var standingState)
            || standingState.CurrentState is StandingState.Standing
            || Transform(uid).GridUid != null)
        {
            return;
        }

        _standing.Stand(uid, standingState);
    }

    public bool TryStandUp(EntityUid uid, LayingDownComponent? layingDown = null, StandingStateComponent? standingState = null)
    {
        if (!Resolve(uid, ref standingState, false) ||
            !Resolve(uid, ref layingDown, false) ||
            standingState.CurrentState is not StandingState.Lying ||
            !_mobState.IsAlive(uid) ||
            _buckle.IsBuckled(uid) ||
            _pulling.IsPulled(uid) ||
            HasComp<LegsParalyzedComponent>(uid) ||
            TerminatingOrDeleted(uid))
        {
            return false;
        }

        var xform = Transform(uid);
        if (xform.GridUid != null)
        {
            foreach (var obj in _map.GetAnchoredEntities(xform.GridUid.Value, Comp<MapGridComponent>(xform.GridUid.Value), xform.Coordinates))
            {
                if (!_tag.HasTag(obj, "Structure"))
                    continue;

                _popup.PopupEntity(Loc.GetString("laying-table-head-dmg",("obj", obj), ("self", uid)), obj, PopupType.MediumCaution);
                _damageable.TryChangeDamage(uid, new DamageSpecifier(){DamageDict = {{"Blunt", 5}}}, ignoreResistances: true);
                _stun.TryStun(uid, TimeSpan.FromSeconds(2), true);
                return false;
            }
        }

        var args = new DoAfterArgs(EntityManager, uid, layingDown.StandingUpTime, new StandingUpDoAfterEvent(), uid)
        {
            BreakOnHandChange = false,
            RequireCanInteract = false
        };

        if (!_doAfter.TryStartDoAfter(args))
            return false;

        standingState.CurrentState = StandingState.GettingUp;
        return true;
    }

    public bool TryLieDown(EntityUid uid, LayingDownComponent? layingDown = null, StandingStateComponent? standingState = null, DropHeldItemsBehavior behavior = DropHeldItemsBehavior.NoDrop)
    {
        if (!Resolve(uid, ref standingState, false) ||
            !Resolve(uid, ref layingDown, false) ||
            standingState.CurrentState is not StandingState.Standing ||
            _buckle.IsBuckled(uid))
        {
            if (behavior == DropHeldItemsBehavior.AlwaysDrop)
                RaiseLocalEvent(uid, new DropHandItemsEvent());

            return false;
        }

        _standing.Down(uid, true, behavior != DropHeldItemsBehavior.NoDrop, standingState: standingState);
        return true;
    }
}

[Serializable, NetSerializable]
public sealed partial class StandingUpDoAfterEvent : SimpleDoAfterEvent;

public enum DropHeldItemsBehavior : byte
{
    NoDrop,
    DropIfStanding,
    AlwaysDrop
}
