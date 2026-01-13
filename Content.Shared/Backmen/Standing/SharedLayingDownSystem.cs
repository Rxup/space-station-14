using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Shared.Backmen.CCVar;
using Content.Shared.Backmen.Targeting;
using Content.Shared.Body.Components;
using Content.Shared.Buckle;
using Content.Shared.Buckle.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Gravity;
using Content.Shared.Input;
using Content.Shared.Interaction;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Movement.Systems;
using Content.Shared.NPC;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Content.Shared.Silicons.Borgs.Components;
using Content.Shared.Standing;
using Content.Shared.Stunnable;
using Content.Shared.Tag;
using Content.Shared.Traits.Assorted;
using Content.Shared.UserInterface;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Containers;
using Robust.Shared.Input.Binding;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Network;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Player;
using Robust.Shared.Serialization;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

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
    [Dependency] private readonly PullingSystem _pulling = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly SharedAudioSystem _audioSystem = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;

    [Dependency] private readonly IConfigurationManager _config = default!;

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

        SubscribeLocalEvent<LayingDownComponent, UnbuckledEvent>(OnUnBuckled);
        SubscribeLocalEvent<LayingDownComponent, StandAttemptEvent>(OnCheckLegs);
        SubscribeLocalEvent<BoundUserInterfaceMessageAttempt>(OnBoundUserInterface, after: [typeof(SharedInteractionSystem)]);
    }

    public bool HasLegs(Entity<LayingDownComponent> ent)
    {
        if (!TryComp<BodyComponent>(ent, out var body))
            return false;

        return HasComp<BorgChassisComponent>(ent) || body.LegEntities.Count >= body.RequiredLegs && body.LegEntities.Count != 0;
    }

    private void OnCheckLegs(Entity<LayingDownComponent> ent, ref StandAttemptEvent args)
    {
        if (!HasLegs(ent))
            args.Cancel();
    }

    private void OnBoundUserInterface(BoundUserInterfaceMessageAttempt args)
    {
        if (args.Cancelled ||
           !TryComp<ActivatableUIComponent>(args.Target, out var uiComp) ||
           !TryComp<StandingStateComponent>(args.Actor, out var standingStateComponent) ||
           standingStateComponent.CurrentState != StandingState.Lying)
            return;

        if (uiComp.RequiresComplex)
            args.Cancel();
    }

    private void OnChangeMobState(Entity<LayingDownComponent> ent, ref MobStateChangedEvent args)
    {
        if (!TryComp<StandingStateComponent>(ent, out var standingStateComponent) ||
            standingStateComponent.Standing)
            return;

        if (args.NewMobState == MobState.Alive)
        {
            if (HasComp<ActiveNPCComponent>(ent))
            {
                TryStandUp(ent, ent, standingStateComponent);
                return;
            }
            AutoGetUp(ent);
            //TryStandUp(ent, ent, standingStateComponent);
        }
    }

    private void OnUnBuckled(Entity<LayingDownComponent> ent, ref UnbuckledEvent args)
    {
        if (!TryComp<StandingStateComponent>(ent, out var standingStateComponent))
            return;

        if (TryComp<BodyComponent>(ent, out var body) &&
            (body.RequiredLegs > 0 && body.LegEntities.Count < body.RequiredLegs || body.LegEntities.Count == 0)
            && standingStateComponent.CurrentState != StandingState.Lying)
        {
            _standing.Down(ent, true, true, true);
            return;
        }

        TryProcessAutoGetUp(ent.AsNullable());
    }

    protected abstract bool GetAutoGetUp(Entity<LayingDownComponent> ent, ICommonSession session);

    public bool TryProcessAutoGetUp(Entity<LayingDownComponent?> ent)
    {
        if (!Resolve(ent, ref ent.Comp, false))
        {
            return false;
        }
        if (_buckle.IsBuckled(ent))
            return false;

        if (_pulling.IsPulled(ent))
            return false;

        if (!IsSafeToStandUp(ent, out _))
            return false;

        var autoUp = !_playerManager.TryGetSessionByEntity(ent, out var player) ||
                     GetAutoGetUp((ent,ent.Comp), session: player);

        if (autoUp && !_container.IsEntityInContainer(ent))
            return TryStandUp(ent, ent);

        return false;
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

        if (!_timing.IsFirstTimePredicted)
            return;

        RaisePredictiveEvent(new ChangeLayingDownEvent());
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

        if (_standing.IsDown((uid, standing)))
            TryStandUp(uid, layingDown, standing);
        else
            TryLieDown(uid, layingDown, standing);

    }

    private void OnStandingUpDoAfter(EntityUid uid, StandingStateComponent component, StandingUpDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled || HasComp<KnockedDownComponent>(uid) ||
            _mobState.IsIncapacitated(uid) || !IsSafeToStandUp(uid, out _) || !_standing.Stand(uid))
        {
            component.CurrentState = StandingState.Lying;
            Dirty(uid,component);
            return;
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

    private const int NotSafeToStandUp = (int)CollisionGroup.MidImpassable | (int)CollisionGroup.BlobImpassable;
    public bool IsSafeToStandUp(EntityUid entity, [NotNullWhen(false)] out EntityUid? obj)
    {
        if(HasComp<ActiveNPCComponent>(entity))
        {
            obj = null;
            return true;
        }
        var xform = Transform(entity);
        if (
            !TryComp<Robust.Shared.Physics.Components.PhysicsComponent>(entity, out var physEnt) ||
            !TryComp<FixturesComponent>(entity, out var fixEnt)
            )
        {
            obj = null;
            return true;
        }

        foreach (var ent in _physics.GetEntitiesIntersectingBody(entity,
                     NotSafeToStandUp,
                     body: physEnt,
                     xform: xform,
                     fixtureComp: fixEnt)
                 )
        {
            if (!TryComp<Robust.Shared.Physics.Components.PhysicsComponent>(ent, out var phys))
                continue;

            if (!phys.CanCollide || !phys.Hard)
                continue;

            if ((phys.CollisionLayer & (int)CollisionGroup.BlobImpassable) != 0 &&
                (physEnt.CollisionMask & (int)CollisionGroup.BlobImpassable) == 0)
                continue;

            if (TryComp<FixturesComponent>(ent, out var fix))
            {
                // slow check
                foreach (var (entFixId, entFix) in fix.Fixtures)
                {
                    if(
                        entFix.Hard &&
                        (entFix.CollisionLayer & NotSafeToStandUp) == 0)
                        continue;

                    if(!entFix.Hard)
                        continue;

                    obj = ent;
                    return false;
                }
            }

            obj = ent;
            return false;
        }

        obj = null;
        return true;
    }

    private static SoundSpecifier _bonkSound = new SoundCollectionSpecifier("TrayHit");
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

        if (!IsSafeToStandUp(uid, out var obj))
        {
            _popup.PopupPredicted(
                Loc.GetString("bonkable-success-message-user",("bonkable", obj.Value)),
                Loc.GetString("bonkable-success-message-others",("bonkable", obj.Value), ("user", uid)),
                obj.Value,
                uid,
                PopupType.MediumCaution);
            _damageable.ChangeDamage(uid, new DamageSpecifier{DamageDict = {{"Blunt", 5}}}, ignoreResistances: true, targetPart: TargetBodyPart.Head);
            _stun.TryAddStunDuration(uid, TimeSpan.FromSeconds(2));
            _audioSystem.PlayPredicted(_bonkSound, uid, obj.Value);
            return false;
        }

        var args = new DoAfterArgs(EntityManager, uid, layingDown.StandingUpTime, new StandingUpDoAfterEvent(), uid)
        {
            BreakOnHandChange = false,
            RequireCanInteract = false
        };

        if (!_doAfter.TryStartDoAfter(args))
            return false;

        standingState.CurrentState = StandingState.GettingUp;
        Dirty(uid, standingState);
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
            {
                var ev = new DropHandItemsEvent();
                RaiseLocalEvent(uid, ref ev);
            }

            return false;
        }

        _standing.Down(uid, true, behavior != DropHeldItemsBehavior.NoDrop, standingState: standingState);
        return true;
    }

    public void LieDownInRange(EntityUid uid, EntityCoordinates coords, float range = 0.4f)
    {
        var ents = new HashSet<Entity<LayingDownComponent>>();
        _lookup.GetEntitiesInRange(coords, range, ents);

        foreach (var ent in ents.Where(ent => ent.Owner != uid))
        {
            TryLieDown(ent, behavior: DropHeldItemsBehavior.DropIfStanding);
        }
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
