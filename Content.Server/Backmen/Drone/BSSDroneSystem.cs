using System.Diagnostics.CodeAnalysis;
using Content.Shared.Backmen.Drone.Actions;
using Content.Server.Body.Systems;
using Content.Server.Chat.Systems;
using Content.Server.Ghost.Roles.Components;
using Content.Server.Hands.Systems;
using Content.Server.Ninja.Events;
using Content.Server.Popups;
using Content.Server.Power.Components;
using Content.Server.Power.EntitySystems;
using Content.Server.PowerCell;
using Content.Server.Radio.Components;
using Content.Server.Radio.EntitySystems;
using Content.Server.Tools.Innate;
using Content.Shared.Actions;
using Content.Shared.Alert;
using Content.Shared.Backmen.Drone;
using Content.Shared.Body.Components;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Emoting;
using Content.Shared.Examine;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs;
using Content.Shared.Ninja.Systems;
using Content.Shared.Popups;
using Content.Shared.PowerCell.Components;
using Content.Shared.Radio;
using Content.Shared.Rounding;
using Content.Shared.Throwing;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Server.Backmen.Drone;

public sealed class BSSDroneSystem : SharedDroneSystem
{
    [Dependency] private readonly BodySystem _bodySystem = default!;
    [Dependency] private readonly PopupSystem _popupSystem = default!;
    [Dependency] private readonly InnateToolSystem _innateToolSystem = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedActionsSystem _action = default!;
    [Dependency] private readonly HandsSystem _handsSystem = default!;
    [Dependency] private readonly RadioSystem _radioSystem = default!;
    [Dependency] private readonly PowerCellSystem _powerCell = default!;
    [Dependency] private readonly BatterySystem _battery = default!;
    [Dependency] private readonly AlertsSystem _alerts = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedBatteryDrainerSystem _batteryDrainer = default!;


    [ValidatePrototypeId<RadioChannelPrototype>]
    public const string BinaryChannel = "Binary";

    public override void Initialize()
    {
        base.Initialize();
        //SubscribeLocalEvent<BSSDroneComponent, MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<BSSDroneComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<BSSDroneComponent, MindAddedMessage>(OnMindAdded);
        SubscribeLocalEvent<BSSDroneComponent, MindRemovedMessage>(OnMindRemoved);
        SubscribeLocalEvent<BSSDroneComponent, EmoteAttemptEvent>(OnEmoteAttempt);
        SubscribeLocalEvent<BSSDroneComponent, ThrowAttemptEvent>(OnThrowAttempt);
        SubscribeLocalEvent<BSSDroneComponent, DroneCraftActionEvent>(OnCraft);
        SubscribeLocalEvent<BSSDroneComponent, ContainerIsInsertingAttemptEvent>(OnInsertAttempt);
        SubscribeLocalEvent<BSSDroneComponent, MapInitEvent>(OnMapInit, after: [typeof(ItemSlotsSystem)]);

        //SubscribeLocalEvent<DroneComponent, EntitySpokeEvent>((uid, _, args) => OnDroneSpeak(uid, args), before: new []{ typeof(RadioSystem) });
        SubscribeLocalEvent<BSSDroneComponent, EntitySpokeEvent>((uid, _, args) => OnDroneSpeak(uid, args), before: new []{ typeof(RadioSystem) });
    }

    private void OnMapInit(Entity<BSSDroneComponent> ent, ref MapInitEvent args)
    {
        if (GetDroneBattery(ent, out var battery, out _))
        {
            _batteryDrainer.SetBattery(ent.Owner, battery);
        }
        else
        {
            Log.Warning($"Drone {ToPrettyString(ent)} battery not found");
        }
    }

    public override void Update(float frameTime)
    {
        var query = EntityQueryEnumerator<BSSDroneComponent>();
        while (query.MoveNext(out var uid, out var droneComponent))
        {
            droneComponent.UpdateTimer += frameTime;
            if (droneComponent.UpdateTimer > 3)
            {
                droneComponent.UpdateTimer = 0;
                SetDronePowerAlert((uid, droneComponent));
            }

        }
    }

    public void SetDronePowerAlert(Entity<BSSDroneComponent> ent)
    {
        var (uid, comp) = ent;
        if (comp.Deleted)
        {
            _alerts.ClearAlert(uid, comp.DronePowerAlert);
            return;
        }

        if (GetDroneBattery(uid, out _, out var battery))
        {
            var severity = ContentHelpers.RoundToLevels(MathF.Max(0f, battery.CurrentCharge), battery.MaxCharge, 8);
            _alerts.ShowAlert(uid, comp.DronePowerAlert, (short) severity);
        }
        else
        {
            _alerts.ClearAlert(uid, comp.DronePowerAlert);
        }
    }

    private void OnInsertAttempt(EntityUid uid, BSSDroneComponent comp, ContainerIsInsertingAttemptEvent args)
    {
        // this is for handling battery upgrading, not stopping actions from being added
        // if another container like ActionsContainer is specified, don't handle it
        if (TryComp<PowerCellSlotComponent>(uid, out var slot) && args.Container.ID != slot.CellSlotId)
            return;

        // no power cell for some reason??? allow it
        if (!_powerCell.TryGetBatteryFromSlot(uid, out var batteryUid, out var battery))
            return;

        if (!TryComp<BatteryComponent>(args.EntityUid, out var inserting))
        {
            args.Cancel();
            return;
        }

        _batteryDrainer.SetBattery(uid, args.EntityUid);
    }

    public bool GetDroneBattery(EntityUid user, [NotNullWhen(true)] out EntityUid? uid, [NotNullWhen(true)] out BatteryComponent? battery)
    {
        if (_powerCell.TryGetBatteryFromSlot(user, out uid, out battery))
        {
            return true;
        }

        uid = null;
        battery = null;
        return false;
    }

    public bool TryUseCharge(EntityUid user, float charge)
    {
        return GetDroneBattery(user, out var uid, out var battery) && _battery.TryUseCharge(uid.Value, charge, battery);
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
/*
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
    }*/

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

    private void OnCraft(EntityUid uid, BSSDroneComponent component, DroneCraftActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        if (!TryUseCharge(args.Performer, args.Energy))
        {
            _popup.PopupEntity(Loc.GetString(component.NoPowerPopup), args.Performer, args.Performer);
            return;
        }

        var item = Spawn(args.CraftedItem, Transform(args.Performer).Coordinates);
        _handsSystem.TryPickupAnyHand(args.Performer, item);
    }
}
