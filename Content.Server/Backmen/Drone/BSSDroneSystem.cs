using Content.Server.Body.Systems;
using Content.Server.Drone.Components;
using Content.Server.Ghost.Components;
using Content.Server.Ghost.Roles.Components;
using Content.Server.Mind.Components;
using Content.Server.Popups;
using Content.Server.Tools.Innate;
using Content.Server.UserInterface;
using Content.Shared.Body.Components;
using Content.Shared.Drone;
using Content.Shared.Emoting;
using Content.Shared.Examine;
using Content.Shared.IdentityManagement;
using Content.Shared.Interaction.Components;
using Content.Shared.Interaction.Events;
using Content.Shared.Item;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Tag;
using Content.Shared.Throwing;
using Robust.Server.GameObjects;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server.Backmen.Drone
{
    public sealed class BSSDroneSystem : SharedDroneSystem
    {
        [Dependency] private readonly BodySystem _bodySystem = default!;
        [Dependency] private readonly PopupSystem _popupSystem = default!;
        [Dependency] private readonly TagSystem _tagSystem = default!;
        [Dependency] private readonly EntityLookupSystem _lookup = default!;
        [Dependency] private readonly IGameTiming _gameTiming = default!;
        [Dependency] private readonly InnateToolSystem _innateToolSystem = default!;
        [Dependency] private readonly MobStateSystem _mobStateSystem = default!;
        [Dependency] private readonly SharedAppearanceSystem _appearance = default!;

        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<BSSDroneComponent, UserOpenActivatableUIAttemptEvent>(OnActivateUIAttempt);
            SubscribeLocalEvent<BSSDroneComponent, MobStateChangedEvent>(OnMobStateChanged);
            SubscribeLocalEvent<BSSDroneComponent, ExaminedEvent>(OnExamined);
            SubscribeLocalEvent<BSSDroneComponent, MindAddedMessage>(OnMindAdded);
            SubscribeLocalEvent<BSSDroneComponent, MindRemovedMessage>(OnMindRemoved);
            SubscribeLocalEvent<BSSDroneComponent, EmoteAttemptEvent>(OnEmoteAttempt);
            SubscribeLocalEvent<BSSDroneComponent, ThrowAttemptEvent>(OnThrowAttempt);
        }

        private void OnActivateUIAttempt(EntityUid uid, BSSDroneComponent component, UserOpenActivatableUIAttemptEvent args)
        {
            if (!_tagSystem.HasTag(args.Target, "DroneUsable"))
            {
                args.Cancel();
            }
        }

        private void OnExamined(EntityUid uid, BSSDroneComponent component, ExaminedEvent args)
        {
            if (TryComp<MindContainerComponent>(uid, out var mind) && mind.HasMind)
            {
                args.PushMarkup(Loc.GetString("drone-active"));
            }
            else
            {
                args.PushMarkup(Loc.GetString("drone-dormant"));
            }
        }

        private void OnMobStateChanged(EntityUid uid, BSSDroneComponent drone, MobStateChangedEvent args)
        {
            if (args.NewMobState == MobState.Dead)
            {
                if (TryComp<InnateToolComponent>(uid, out var innate))
                    _innateToolSystem.Cleanup(uid, innate);

                if (TryComp<BodyComponent>(uid, out var body))
                    _bodySystem.GibBody(uid, body: body);
                QueueDel(uid);
            }
        }

        private void OnMindAdded(EntityUid uid, BSSDroneComponent drone, MindAddedMessage args)
        {
            UpdateDroneAppearance(uid, DroneStatus.On);
            _popupSystem.PopupEntity(Loc.GetString("drone-activated"), uid, PopupType.Large);
        }

        private void OnMindRemoved(EntityUid uid, BSSDroneComponent drone, MindRemovedMessage args)
        {
            UpdateDroneAppearance(uid, DroneStatus.Off);
            EnsureComp<GhostTakeoverAvailableComponent>(uid);
        }

        private void OnEmoteAttempt(EntityUid uid, BSSDroneComponent component, EmoteAttemptEvent args)
        {
            // No.
            args.Cancel();
        }

        private void OnThrowAttempt(EntityUid uid, BSSDroneComponent drone, ThrowAttemptEvent args)
        {
            args.Cancel();
        }

        private void UpdateDroneAppearance(EntityUid uid, DroneStatus status)
        {
            if (TryComp<AppearanceComponent>(uid, out var appearance))
            {
                _appearance.SetData(uid, DroneVisuals.Status, status, appearance);
            }
        }
    }
}
