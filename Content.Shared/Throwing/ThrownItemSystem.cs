using System.Linq;
using Content.Shared.Administration.Logs;
using Content.Shared.Database;
using Content.Shared.Hands.Components;
using Content.Shared.Physics;
using Content.Shared.Physics.Pull;
using Robust.Shared.Containers;
using Robust.Shared.GameStates;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Events;
using Robust.Shared.Physics.Systems;

namespace Content.Shared.Throwing
{
    /// <summary>
    ///     Handles throwing landing and collisions.
    /// </summary>
    public sealed class ThrownItemSystem : EntitySystem
    {
        [Dependency] private readonly ISharedAdminLogManager _adminLogger = default!;
        [Dependency] private readonly SharedBroadphaseSystem _broadphase = default!;
        [Dependency] private readonly SharedContainerSystem _containerSystem = default!;
        [Dependency] private readonly FixtureSystem _fixtures = default!;
        [Dependency] private readonly SharedPhysicsSystem _physics = default!;

        private const string ThrowingFixture = "throw-fixture";

        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<ThrownItemComponent, PhysicsSleepEvent>(OnSleep);
            SubscribeLocalEvent<ThrownItemComponent, StartCollideEvent>(HandleCollision);
            SubscribeLocalEvent<ThrownItemComponent, PreventCollideEvent>(PreventCollision);
            SubscribeLocalEvent<ThrownItemComponent, ThrownEvent>(ThrowItem);
            SubscribeLocalEvent<ThrownItemComponent, ComponentGetState>(OnGetState);
            SubscribeLocalEvent<ThrownItemComponent, ComponentHandleState>(OnHandleState);
            SubscribeLocalEvent<PullStartedMessage>(HandlePullStarted);
        }

        private void OnGetState(EntityUid uid, ThrownItemComponent component, ref ComponentGetState args)
        {
            args.State = new ThrownItemComponentState(component.Thrower);
        }

        private void OnHandleState(EntityUid uid, ThrownItemComponent component, ref ComponentHandleState args)
        {
            if (args.Current is not ThrownItemComponentState {Thrower: not null } state ||
                !state.Thrower.Value.IsValid())
            {
                return;
            }

            component.Thrower = state.Thrower.Value;
        }

        private void ThrowItem(EntityUid uid, ThrownItemComponent component, ThrownEvent args)
        {
            if (!EntityManager.TryGetComponent(uid, out FixturesComponent? fixturesComponent) ||
                fixturesComponent.Fixtures.Count != 1 ||
                !TryComp<PhysicsComponent>(uid, out var body))
            {
                return;
            }

            var fixture = fixturesComponent.Fixtures.Values.First();
            var shape = fixture.Shape;
            _fixtures.TryCreateFixture(uid, shape, ThrowingFixture, hard: false, collisionMask: (int) CollisionGroup.ThrownItem, manager: fixturesComponent, body: body);
        }

        private void HandleCollision(EntityUid uid, ThrownItemComponent component, ref StartCollideEvent args)
        {
            if (args.OtherFixture.Hard == false)
                return;

            var thrower = component.Thrower;
            if (args.OtherEntity == thrower)
                return;

            ThrowCollideInteraction(thrower, args.OurBody, args.OtherBody);
        }

        private void PreventCollision(EntityUid uid, ThrownItemComponent component, ref PreventCollideEvent args)
        {
            if (args.BodyB.Owner == component.Thrower)
            {
                args.Cancelled = true;
            }
        }

        private void OnSleep(EntityUid uid, ThrownItemComponent thrownItem, ref PhysicsSleepEvent @event)
        {
            StopThrow(uid, thrownItem);
        }

        private void HandlePullStarted(PullStartedMessage message)
        {
            // TODO: this isn't directed so things have to be done the bad way
            if (EntityManager.TryGetComponent(message.Pulled.Owner, out ThrownItemComponent? thrownItemComponent))
                StopThrow(message.Pulled.Owner, thrownItemComponent);
        }

        private void StopThrow(EntityUid uid, ThrownItemComponent thrownItemComponent)
        {
            if (EntityManager.TryGetComponent(uid, out FixturesComponent? manager))
            {
                var fixture = _fixtures.GetFixtureOrNull(uid, ThrowingFixture, manager: manager);

                if (fixture != null)
                {
                    _fixtures.DestroyFixture(uid, fixture, manager: manager);
                }
            }

            EntityManager.EventBus.RaiseLocalEvent(uid, new StopThrowEvent {User = thrownItemComponent.Thrower}, true);
            EntityManager.RemoveComponent<ThrownItemComponent>(uid);
        }

        public void LandComponent(EntityUid uid, ThrownItemComponent thrownItem, PhysicsComponent physics, bool playSound)
        {
            _physics.SetBodyStatus(physics, BodyStatus.OnGround);

            if (thrownItem.Deleted || Deleted(uid) || _containerSystem.IsEntityInContainer(uid))
                return;

            var landing = uid;

            // Unfortunately we can't check for hands containers as they have specific names.
            if (uid.TryGetContainerMan(out var containerManager) &&
                EntityManager.HasComponent<HandsComponent>(containerManager.Owner))
            {
                EntityManager.RemoveComponent(landing, thrownItem);
                return;
            }

            // Assume it's uninteresting if it has no thrower. For now anyway.
            if (thrownItem.Thrower is not null)
                _adminLogger.Add(LogType.Landed, LogImpact.Low, $"{ToPrettyString(landing):entity} thrown by {ToPrettyString(thrownItem.Thrower.Value):thrower} landed.");

            _broadphase.RegenerateContacts(uid, physics);
            var landEvent = new LandEvent(thrownItem.Thrower, playSound);
            RaiseLocalEvent(landing, ref landEvent);
        }

        /// <summary>
        ///     Raises collision events on the thrown and target entities.
        /// </summary>
        public void ThrowCollideInteraction(EntityUid? user, PhysicsComponent thrown, PhysicsComponent target)
        {
            if (user is not null)
                _adminLogger.Add(LogType.ThrowHit, LogImpact.Low,
                    $"{ToPrettyString(thrown.Owner):thrown} thrown by {ToPrettyString(user.Value):thrower} hit {ToPrettyString(target.Owner):target}.");
            // TODO: Just pass in the bodies directly
            RaiseLocalEvent(target.Owner, new ThrowHitByEvent(user, thrown.Owner, target.Owner), true);
            RaiseLocalEvent(thrown.Owner, new ThrowDoHitEvent(user, thrown.Owner, target.Owner), true);
        }
    }
}
