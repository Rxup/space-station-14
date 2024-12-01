using System.Linq;
using Content.Server.Construction.Completions;
using Content.Server.Popups;
using Content.Shared.VentCraw.Tube.Components;
using Content.Shared.VentCraw.Components;
using Content.Shared.Tools.Components;
using Content.Shared.Destructible;
using Content.Shared.DoAfter;
using Content.Shared.Movement.Systems;
using Content.Shared.VentCraw;
using Content.Shared.Verbs;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Server.VentCraw
{
    public sealed class VentCrawTubeSystem : EntitySystem
    {
        [Dependency] private readonly SharedVentCrawableSystem _ventCrawableSystem = default!;
        [Dependency] private readonly SharedContainerSystem _containerSystem = default!;
        [Dependency] private readonly SharedDoAfterSystem _doAfterSystem = default!;
        [Dependency] private readonly PopupSystem _popup = default!;
        [Dependency] private readonly SharedMoverController _mover = default!;
        [Dependency] private readonly SharedTransformSystem _transform = default!;

        public override void Initialize()
        {
            base.Initialize();

            SubscribeLocalEvent<VentCrawTubeComponent, ComponentInit>(OnComponentInit);
            SubscribeLocalEvent<VentCrawTubeComponent, ComponentRemove>(OnComponentRemove);

            SubscribeLocalEvent<VentCrawTubeComponent, AnchorStateChangedEvent>(OnAnchorChange);
            SubscribeLocalEvent<VentCrawTubeComponent, BreakageEventArgs>(OnBreak);
            SubscribeLocalEvent<VentCrawTubeComponent, ComponentShutdown>(OnShutdown);
            SubscribeLocalEvent<VentCrawTubeComponent, ComponentStartup>(OnStartup);
            SubscribeLocalEvent<VentCrawTubeComponent, ConstructionBeforeDeleteEvent>(OnDeconstruct);
            SubscribeLocalEvent<VentCrawBendComponent, GetVentCrawsConnectableDirectionsEvent>(OnGetBendConnectableDirections);
            SubscribeLocalEvent<VentCrawEntryComponent, GetVentCrawsConnectableDirectionsEvent>(OnGetEntryConnectableDirections);
            SubscribeLocalEvent<VentCrawJunctionComponent, GetVentCrawsConnectableDirectionsEvent>(OnGetJunctionConnectableDirections);
            SubscribeLocalEvent<VentCrawTransitComponent, GetVentCrawsConnectableDirectionsEvent>(OnGetTransitConnectableDirections);
            SubscribeLocalEvent<VentCrawEntryComponent, GetVerbsEvent<AlternativeVerb>>(AddClimbedVerb);
            SubscribeLocalEvent<VentCrawlerComponent, EnterVentDoAfterEvent>(OnDoAfterEnterTube);
        }

        private void AddClimbedVerb(EntityUid uid, VentCrawEntryComponent component, GetVerbsEvent<AlternativeVerb> args)
        {
            if (!TryComp<VentCrawlerComponent>(args.User, out var ventCrawlerComponent) || HasComp<BeingVentCrawComponent>(args.User))
                return;

            var xform = Transform(uid);
            if (!xform.Anchored)
                return;

            AlternativeVerb verb = new()
            {
                Act = () => TryEnter(uid, args.User, ventCrawlerComponent),
                Text = Loc.GetString("comp-climbable-verb-climb")
            };
            args.Verbs.Add(verb);
        }

        private void OnDoAfterEnterTube(EntityUid uid, VentCrawlerComponent component, EnterVentDoAfterEvent args)
        {
            if (args.Handled || args.Cancelled || args.Args.Target == null || args.Args.Used == null)
                return;

            TryInsert(args.Args.Target.Value, args.Args.Used.Value);

            args.Handled = true;
        }

        private void TryEnter(EntityUid uid, EntityUid user, VentCrawlerComponent crawler)
        {
            if (TryComp<WeldableComponent>(uid, out var weldableComponent))
            {
                if (weldableComponent.IsWelded)
                {
                    _popup.PopupEntity(Loc.GetString("entity-storage-component-welded-shut-message"), user);
                    return;
                }
            }

            var args = new DoAfterArgs(EntityManager, user, crawler.EnterDelay, new EnterVentDoAfterEvent(), user, uid, user)
            {
                BreakOnMove = true,
                BreakOnDamage = false
            };

            _doAfterSystem.TryStartDoAfter(args);
        }

        private void OnComponentInit(EntityUid uid, VentCrawTubeComponent tube, ComponentInit args)
        {
            tube.Contents = _containerSystem.EnsureContainer<Container>(uid, tube.ContainerId);
        }

        private void OnComponentRemove(EntityUid uid, VentCrawTubeComponent tube, ComponentRemove args)
        {
            DisconnectTube(uid, tube);
        }

        private void OnShutdown(EntityUid uid, VentCrawTubeComponent tube, ComponentShutdown args)
        {
            DisconnectTube(uid, tube);
        }

        private void OnGetBendConnectableDirections(EntityUid uid, VentCrawBendComponent component, ref GetVentCrawsConnectableDirectionsEvent args)
        {
            var direction = Transform(uid).LocalRotation;
            var side = new Angle(MathHelper.DegreesToRadians(direction.Degrees - 90));

            args.Connectable = new[] { direction.GetDir(), side.GetDir() };
        }

        private void OnGetEntryConnectableDirections(EntityUid uid, VentCrawEntryComponent component, ref GetVentCrawsConnectableDirectionsEvent args)
        {
            args.Connectable = new[] { Transform(uid).LocalRotation.GetDir() };
        }

        private void OnGetJunctionConnectableDirections(EntityUid uid, VentCrawJunctionComponent component, ref GetVentCrawsConnectableDirectionsEvent args)
        {
            var direction = Transform(uid).LocalRotation;

            args.Connectable = component.Degrees
                .Select(degree => new Angle(degree.Theta + direction.Theta).GetDir())
                .ToArray();
        }

        private void OnGetTransitConnectableDirections(EntityUid uid, VentCrawTransitComponent component, ref GetVentCrawsConnectableDirectionsEvent args)
        {
            var rotation = Transform(uid).LocalRotation;
            var opposite = new Angle(rotation.Theta + Math.PI);

            args.Connectable = new[] { rotation.GetDir(), opposite.GetDir() };
        }

        private void OnDeconstruct(EntityUid uid, VentCrawTubeComponent component, ConstructionBeforeDeleteEvent args)
        {
            DisconnectTube(uid, component);
        }

        private void OnStartup(EntityUid uid, VentCrawTubeComponent component, ComponentStartup args)
        {
            UpdateAnchored(uid, component, Transform(uid).Anchored);
        }

        private void OnBreak(EntityUid uid, VentCrawTubeComponent component, BreakageEventArgs args)
        {
            DisconnectTube(uid, component);
        }

        private void OnAnchorChange(EntityUid uid, VentCrawTubeComponent component, ref AnchorStateChangedEvent args)
        {
            UpdateAnchored(uid, component, args.Anchored);
        }

        private void UpdateAnchored(EntityUid uid, VentCrawTubeComponent component, bool anchored)
        {
            if (anchored)
            {
                ConnectTube(uid, component);
            }
            else
            {
                DisconnectTube(uid, component);
            }
        }

        private static void ConnectTube(EntityUid _, VentCrawTubeComponent tube)
        {
            if (tube.Connected)
            {
                return;
            }

            tube.Connected = true;
        }


        private void DisconnectTube(EntityUid _, VentCrawTubeComponent tube)
        {
            if (!tube.Connected)
            {
                return;
            }

            tube.Connected = false;

            var query = GetEntityQuery<VentCrawHolderComponent>();
            foreach (var entity in tube.Contents.ContainedEntities.ToArray())
            {
                if (query.TryGetComponent(entity, out var holder))
                {
                    var Exitev = new VentCrawExitEvent();
                    RaiseLocalEvent(entity, ref Exitev);
                }
            }
        }

        private bool TryInsert(EntityUid uid, EntityUid entity, VentCrawEntryComponent? entry = null)
        {
            if (!Resolve(uid, ref entry))
                return false;

            if (!TryComp<VentCrawlerComponent>(entity, out var ventCrawlerComponent))
                return false;

            var holder = Spawn(VentCrawEntryComponent.HolderPrototypeId, _transform.GetMapCoordinates(uid));
            var holderComponent = Comp<VentCrawHolderComponent>(holder);

            _ventCrawableSystem.TryInsert(holder, entity, holderComponent);

            _mover.SetRelay(entity, holder);
            ventCrawlerComponent.InTube = true;
            Dirty(entity, ventCrawlerComponent);

            return _ventCrawableSystem.EnterTube(holder, uid, holderComponent);
        }
    }
}
