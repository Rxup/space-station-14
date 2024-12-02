using Content.Server.Backmen.Cocoon;
using Content.Shared.Backmen.Arachne;
using Content.Shared.Actions;
using Content.Shared.Coordinates.Helpers;
using Content.Shared.IdentityManagement;
using Content.Shared.Verbs;
using Content.Shared.Buckle.Components;
using Content.Shared.Maps;
using Content.Shared.DoAfter;
using Content.Shared.Physics;
using Content.Shared.Stunnable;
using Content.Shared.Eye.Blinding.Systems;
using Content.Shared.Doors.Components;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Damage;
using Content.Shared.Inventory;
using Content.Shared.Administration.Logs;
using Content.Shared.Database;
using Content.Shared.Examine;
using Content.Shared.Humanoid;
using Content.Shared.Nutrition.Components;
using Content.Shared.Nutrition.EntitySystems;
using Content.Server.Buckle.Systems;
using Content.Server.Popups;
using Content.Server.DoAfter;
using Content.Server.Body.Components;
using Content.Server.Backmen.Vampiric;
using Content.Server.Speech.Components;
using Content.Shared.Backmen.Abilities;
using Content.Shared.Backmen.Vampiric.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Player;
using Robust.Shared.Physics.Components;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Utility;
using Robust.Server.Console;
using Robust.Shared.Map.Components;
using static Content.Shared.Examine.ExamineSystemShared;

namespace Content.Server.Backmen.Arachne;

public sealed class ArachneSystem : EntitySystem
{
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly HungerSystem _hungerSystem = default!;
    [Dependency] private readonly ThirstSystem _thirstSystem = default!;
    [Dependency] private readonly PopupSystem _popupSystem = default!;
    [Dependency] private readonly DoAfterSystem _doAfter = default!;
    [Dependency] private readonly BuckleSystem _buckleSystem = default!;
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;
    [Dependency] private readonly BlindableSystem _blindableSystem = default!;
    [Dependency] private readonly DamageableSystem _damageableSystem = default!;

    [Dependency] private readonly IServerConsoleHost _host = default!;
    [Dependency] private readonly BloodSuckerSystem _bloodSuckerSystem = default!;
    [Dependency] private readonly InventorySystem _inventorySystem = default!;
    [Dependency] private readonly ISharedAdminLogManager _adminLogger = default!;
    [Dependency] private readonly SharedContainerSystem _containerSystem = default!;
    [Dependency] private readonly ExamineSystemShared _examine = default!;


    private const string BodySlot = "body_slot";

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ArachneComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<ArachneComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<SpinWebActionEvent>(OnSpinWeb);
        SubscribeLocalEvent<ArachneComponent, ArachneWebDoAfterEvent>(OnWebDoAfter);
    }

    private void OnShutdown(EntityUid uid, ArachneComponent component, ComponentShutdown args)
    {
        _actions.RemoveAction(uid, component.SpinWeb);
    }


    [ValidatePrototypeId<EntityPrototype>] private const string ActionSpinWeb = "ActionSpinWeb";

    private void OnInit(EntityUid uid, ArachneComponent component, ComponentInit args)
    {
        _actions.AddAction(uid, ref component.SpinWeb, ActionSpinWeb);
    }



    private void OnEntRemoved(EntityUid uid, WebComponent web, EntRemovedFromContainerMessage args)
    {
        if (!TryComp<StrapComponent>(uid, out var strap))
            return;

        if (HasComp<ArachneComponent>(args.Entity))
            _buckleSystem.StrapSetEnabled(uid, false, strap);
    }

    private void OnSpinWeb(SpinWebActionEvent args)
    {
        if (!TryComp<ArachneComponent>(args.Performer, out var arachne))
            return;

        if (_containerSystem.IsEntityInContainer(args.Performer))
            return;

        TryComp<HungerComponent>(args.Performer, out var hunger);
        TryComp<ThirstComponent>(args.Performer, out var thirst);

        if (hunger != null && thirst != null)
        {
            if (hunger.CurrentThreshold <= Shared.Nutrition.Components.HungerThreshold.Peckish)
            {
                _popupSystem.PopupEntity(Loc.GetString("spin-web-action-hungry"),
                    args.Performer,
                    args.Performer,
                    Shared.Popups.PopupType.MediumCaution);
                return;
            }

            if (thirst.CurrentThirstThreshold <= ThirstThreshold.Thirsty)
            {
                _popupSystem.PopupEntity(Loc.GetString("spin-web-action-thirsty"),
                    args.Performer,
                    args.Performer,
                    Shared.Popups.PopupType.MediumCaution);
                return;
            }
        }

        var coords = args.Target;

        if (!HasComp<MapGridComponent>(_transform.GetGrid(coords)))
        {
            _popupSystem.PopupEntity(Loc.GetString("action-name-spin-web-space"),
                args.Performer,
                args.Performer,
                Shared.Popups.PopupType.MediumCaution);
            return;
        }


        foreach (var entity in _lookup.GetEntitiesIntersecting(coords))
        {
            PhysicsComponent? physics = null; // We use this to check if it's impassable
            if ((HasComp<WebComponent>(entity)) || // Is there already a web there?
                ((Resolve(entity, ref physics, false) &&
                  (physics.CollisionLayer & (int)CollisionGroup.Impassable) != 0) // Is it impassable?
                 && !(TryComp<DoorComponent>(entity, out var door) &&
                      door.State != DoorState.Closed))) // Is it a door that's open and so not actually impassable?
            {
                _popupSystem.PopupEntity(Loc.GetString("action-name-spin-web-blocked"),
                    args.Performer,
                    args.Performer,
                    Shared.Popups.PopupType.MediumCaution);
                return;
            }
        }

        _popupSystem.PopupEntity(
            Loc.GetString("spin-web-start-third-person", ("spider", Identity.Entity(args.Performer, EntityManager))),
            args.Performer,
            Filter.PvsExcept(args.Performer)
                .RemoveWhereAttachedEntity(entity =>
                    !_examine.InRangeUnOccluded(args.Performer, entity, ExamineRange, null)),
            true,
            Shared.Popups.PopupType.MediumCaution);
        _popupSystem.PopupEntity(Loc.GetString("spin-web-start-second-person"),
            args.Performer,
            args.Performer,
            Shared.Popups.PopupType.Medium);

        var ev = new ArachneWebDoAfterEvent(GetNetCoordinates(coords));
        var doAfterArgs = new DoAfterArgs(EntityManager, args.Performer, arachne.WebDelay, ev, args.Performer)
        {
            BreakOnMove = true,
        };

        _doAfter.TryStartDoAfter(doAfterArgs);
    }

    [ValidatePrototypeId<EntityPrototype>] private const string ArachneWeb = "ArachneWeb";

    private void OnWebDoAfter(EntityUid uid, ArachneComponent component, ArachneWebDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled)
            return;

        _hungerSystem.ModifyHunger(uid, -8);
        if (TryComp<ThirstComponent>(uid, out var thirst))
            _thirstSystem.ModifyThirst(uid, thirst, -20);

        Spawn(ArachneWeb, GetCoordinates(args.Coords).SnapToGrid());
        _popupSystem.PopupEntity(
            Loc.GetString("spun-web-third-person", ("spider", Identity.Entity(uid, EntityManager))),
            uid,
            Filter.PvsExcept(uid)
                .RemoveWhereAttachedEntity(entity => !_examine.InRangeUnOccluded(uid, entity, ExamineRange, null)),
            true,
            Shared.Popups.PopupType.MediumCaution);
        _popupSystem.PopupEntity(Loc.GetString("spun-web-second-person"), uid, uid, Shared.Popups.PopupType.Medium);
        args.Handled = true;
    }

}
