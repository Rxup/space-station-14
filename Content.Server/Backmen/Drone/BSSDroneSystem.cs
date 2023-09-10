using Content.Server.Backmen.Drone.Actions;
using Content.Server.Body.Systems;
using Content.Server.Chat.Systems;
using Content.Server.Drone.Components;
using Content.Server.Ghost.Roles.Components;
using Content.Server.Hands.Systems;
using Content.Server.Popups;
using Content.Server.Radio.Components;
using Content.Server.Radio.EntitySystems;
using Content.Server.Tools.Innate;
using Content.Shared.Actions;
using Content.Shared.Actions.ActionTypes;
using Content.Shared.Body.Components;
using Content.Shared.Drone;
using Content.Shared.Emoting;
using Content.Shared.Examine;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Radio;
using Content.Shared.Tag;
using Content.Shared.Throwing;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.Backmen.Drone;

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
    [Dependency] private readonly SharedActionsSystem _action = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly HandsSystem _handsSystem = default!;
    [Dependency] private readonly RadioSystem _radioSystem = default!;


    [ValidatePrototypeId<RadioChannelPrototype>]
    public const string BinaryChannel = "Binary";

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BSSDroneComponent, MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<BSSDroneComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<BSSDroneComponent, MindAddedMessage>(OnMindAdded);
        SubscribeLocalEvent<BSSDroneComponent, MindRemovedMessage>(OnMindRemoved);
        SubscribeLocalEvent<BSSDroneComponent, EmoteAttemptEvent>(OnEmoteAttempt);
        SubscribeLocalEvent<BSSDroneComponent, ThrowAttemptEvent>(OnThrowAttempt);
        SubscribeLocalEvent<BSSDroneComponent, ComponentStartup>(OnComponentStartup);

        SubscribeLocalEvent<BSSDroneComponent, bloodpackCraftActionEvent>(OnCraftBloodpack);
        SubscribeLocalEvent<BSSDroneComponent, ointmentCraftActionEvent>(OnCraftOintment);
        SubscribeLocalEvent<BSSDroneComponent, brutepackCraftActionEvent>(OnCraftBrutepack);

        SubscribeLocalEvent<DroneComponent, EntitySpokeEvent>((uid, _, args) => OnDroneSpeak(uid, args), before: new []{ typeof(RadioSystem) });
        SubscribeLocalEvent<BSSDroneComponent, EntitySpokeEvent>((uid, _, args) => OnDroneSpeak(uid, args), before: new []{ typeof(RadioSystem) });
    }

    private void OnDroneSpeak(EntityUid uid, EntitySpokeEvent args, IntrinsicRadioTransmitterComponent? component = null)
    {
        if (args.Channel == null ||
            !Resolve(uid, ref component, false) ||
            !component.Channels.Contains(args.Channel.ID) ||
            args.Channel.ID != BinaryChannel)
        {
            return;
        }

        _radioSystem.SendRadioMessage(uid, args.OriginalMessage, args.Channel, uid);
        args.Channel = null; // prevent duplicate messages from other listeners.
    }

    #region Base Drone

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
    #endregion

    #region Med Drone Actions

    private void OnComponentStartup(EntityUid uid, BSSDroneComponent component, ComponentStartup args)
    {
        if (component.DroneType != "MED")
        {
            return;
        }

        if (_prototypes.TryIndex<InstantActionPrototype>("BPLAMEDActionBrutepack", out var action1))
        {
            var netAction = new InstantAction(action1);
            _action.AddAction(uid, netAction, null);

        }
        if (_prototypes.TryIndex<InstantActionPrototype>("BPLAMEDActionOintment", out var action2))
        {
            var netAction = new InstantAction(action2);
            _action.AddAction(uid, netAction, null);

        }
        if (_prototypes.TryIndex<InstantActionPrototype>("BPLAMEDActionBloodpack", out var action3))
        {
            var netAction = new InstantAction(action3);
            _action.AddAction(uid, netAction, null);

        }
    }

    private void OnCraftBrutepack(EntityUid uid, BSSDroneComponent component, brutepackCraftActionEvent args)
    {
        if (args.Handled)
        {
            return;
        }

        var item = Spawn("Brutepack", Transform(args.Performer).Coordinates);
        _handsSystem.TryPickupAnyHand(args.Performer, item);

        args.Handled = true;
    }

    private void OnCraftOintment(EntityUid uid, BSSDroneComponent component, ointmentCraftActionEvent args)
    {
        if (args.Handled)
        {
            return;
        }

        var item = Spawn("Ointment", Transform(args.Performer).Coordinates);
        _handsSystem.TryPickupAnyHand(args.Performer, item);

        args.Handled = true;
    }

    private void OnCraftBloodpack(EntityUid uid, BSSDroneComponent component, bloodpackCraftActionEvent args)
    {
        if (args.Handled)
        {
            return;
        }

        var item = Spawn("Bloodpack", Transform(args.Performer).Coordinates);
        _handsSystem.TryPickupAnyHand(args.Performer, item);

        args.Handled = true;
    }
    #endregion

}
