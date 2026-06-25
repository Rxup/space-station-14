using Content.Server.Backmen.Cocoon;
using Content.Shared.Backmen.Arachne;
using Content.Shared.Backmen.Body.Systems;
using Content.Shared.Actions;
using Content.Shared.Body;
using Content.Shared.Body.Organ;
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
using Content.Shared.Damage.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Player;
using Robust.Shared.Random;
using Robust.Shared.Physics.Components;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Utility;
using Robust.Server.Console;
using static Content.Shared.Examine.ExamineSystemShared;

namespace Content.Server.Backmen.Arachne;

public sealed partial class ArachneSystem : EntitySystem
{
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private HungerSystem _hungerSystem = default!;
    [Dependency] private ThirstSystem _thirstSystem = default!;
    [Dependency] private PopupSystem _popupSystem = default!;
    [Dependency] private DoAfterSystem _doAfter = default!;
    [Dependency] private BuckleSystem _buckleSystem = default!;
    [Dependency] private ItemSlotsSystem _itemSlots = default!;
    [Dependency] private BlindableSystem _blindableSystem = default!;
    [Dependency] private DamageableSystem _damageableSystem = default!;

    [Dependency] private IServerConsoleHost _host = default!;
    [Dependency] private BloodSuckerSystem _bloodSuckerSystem = default!;
    [Dependency] private InventorySystem _inventorySystem = default!;
    [Dependency] private ISharedAdminLogManager _adminLogger = default!;
    [Dependency] private SharedContainerSystem _containerSystem = default!;
    [Dependency] private ExamineSystemShared _examine = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private BkmBodySharedSystem _body = default!;

    private static readonly Vector2i[] NeighborOffsets =
    [
        new(1, 0),
        new(-1, 0),
        new(0, 1),
        new(0, -1),
        new(1, 1),
        new(1, -1),
        new(-1, 1),
        new(-1, -1),
    ];


    private const string BodySlot = "body_slot";

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ArachneComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<ArachneComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<SpinWebActionEvent>(OnSpinWeb);
        SubscribeLocalEvent<ArachneComponent, ArachneWebDoAfterEvent>(OnWebDoAfter);

        SubscribeLocalEvent<ArachneOrganComponent, OrganGotInsertedEvent>(OnArachneOrganInserted);
        SubscribeLocalEvent<ArachneOrganComponent, OrganGotRemovedEvent>(OnArachneOrganRemoved);
    }

    private void OnArachneOrganInserted(Entity<ArachneOrganComponent> ent, ref OrganGotInsertedEvent args)
    {
        SyncArachneComponent(args.Target);
    }

    private void OnArachneOrganRemoved(Entity<ArachneOrganComponent> ent, ref OrganGotRemovedEvent args)
    {
        SyncArachneComponent(args.Target);
    }

    private void SyncArachneComponent(EntityUid body)
    {
        if (_body.BodyHasArachneOrgan(body))
        {
            EnsureComp<ArachneComponent>(body);
            EnsureComp<ArachneClothingStencilComponent>(body);
        }
        else
        {
            RemComp<ArachneClothingStencilComponent>(body);
            RemComp<ArachneComponent>(body);
        }
    }

    private void OnShutdown(EntityUid uid, ArachneComponent component, ComponentShutdown args)
    {
        _actions.RemoveAction(uid, component.SpinWeb);
    }


    private readonly EntProtoId ActionSpinWeb = "ActionSpinWeb";

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

    public bool NPCTrySpinWeb(EntityUid uid, ArachneComponent? arachne = null)
    {
        if (!Resolve(uid, ref arachne, false))
            return false;

        return NPCTrySpinWebAt(uid, Transform(uid).Coordinates.SnapToGrid(EntityManager), arachne);
    }

    public bool NPCTrySpinWebAt(EntityUid uid, EntityCoordinates coords, ArachneComponent? arachne = null)
    {
        if (!Resolve(uid, ref arachne, false))
            return false;

        if (_containerSystem.IsEntityInContainer(uid))
            return false;

        coords = coords.SnapToGrid(EntityManager);

        if (!CanSpinWebAt(uid, coords, false))
            return false;

        var ev = new ArachneWebDoAfterEvent(GetNetCoordinates(coords));
        var doAfterArgs = new DoAfterArgs(EntityManager, uid, arachne.NpcWebDelay, ev, uid)
        {
            BreakOnMove = true,
        };

        return _doAfter.TryStartDoAfter(doAfterArgs);
    }

    public bool TryPickNearbyWebTile(EntityCoordinates origin, float maxRange, out EntityCoordinates coords)
    {
        coords = default;

        if (!TryGetGridData(origin, out var gridUid, out _, out var center))
            return false;

        var current = origin.SnapToGrid(EntityManager);
        var radius = (int) Math.Ceiling(maxRange);
        var candidates = new List<EntityCoordinates>();
        var others = new List<EntityCoordinates>();

        for (var x = -radius; x <= radius; x++)
        {
            for (var y = -radius; y <= radius; y++)
            {
                if (Math.Abs(x) + Math.Abs(y) > maxRange)
                    continue;

                var candidate = new EntityCoordinates(gridUid, center + new Vector2i(x, y)).SnapToGrid(EntityManager);

                if (!IsTileWebbed(candidate))
                    continue;

                candidates.Add(candidate);

                if (candidate != current)
                    others.Add(candidate);
            }
        }

        if (candidates.Count == 0)
            return false;

        coords = others.Count > 0 ? _random.Pick(others) : _random.Pick(candidates);
        return true;
    }

    public int CountWebbedTilesInRadius(EntityCoordinates origin, int radius)
    {
        if (radius <= 0)
            return IsTileWebbed(origin.SnapToGrid(EntityManager)) ? 1 : 0;

        if (!TryGetGridData(origin, out var gridUid, out var grid, out var center))
            return 0;

        var count = 0;

        for (var x = -radius; x <= radius; x++)
        {
            for (var y = -radius; y <= radius; y++)
            {
                var coords = new EntityCoordinates(gridUid, center + new Vector2i(x, y)).SnapToGrid(EntityManager);

                if (IsTileWebbed(coords))
                    count++;
            }
        }

        return count;
    }

    public bool IsWebNestReady(EntityCoordinates origin, int expandRadius, int minWebTiles)
    {
        expandRadius = Math.Max(expandRadius, 1);
        minWebTiles = Math.Max(minWebTiles, 1);
        var coords = origin.SnapToGrid(EntityManager);

        return CountWebbedTilesInRadius(coords, expandRadius) >= minWebTiles && IsTileWebbed(coords);
    }

    /// <summary>
    /// Finds the next tile to extend the local web network.
    /// </summary>
    public bool TryGetNextExpandableWebTile(EntityUid uid, EntityCoordinates origin, int radius, out EntityCoordinates coords)
    {
        coords = default;

        if (!TryGetGridData(origin, out var gridUid, out var grid, out var center))
            return false;

        var current = origin.SnapToGrid(EntityManager);
        var bestDist = int.MaxValue;
        EntityCoordinates? best = null;

        void Consider(EntityCoordinates candidate, int dist)
        {
            if (IsTileWebbed(candidate) || !CanSpinWebAt(uid, candidate, false))
                return;

            if (dist >= bestDist)
                return;

            bestDist = dist;
            best = candidate;
        }

        for (var x = -radius; x <= radius; x++)
        {
            for (var y = -radius; y <= radius; y++)
            {
                if (x == 0 && y == 0)
                    continue;

                var candidate = new EntityCoordinates(gridUid, center + new Vector2i(x, y)).SnapToGrid(EntityManager);

                if (!IsAdjacentToWebbedTile(candidate))
                    continue;

                Consider(candidate, Math.Abs(x) + Math.Abs(y));
            }
        }

        if (best != null)
        {
            coords = best.Value;
            return true;
        }

        if (!IsTileWebbed(current) && CanSpinWebAt(uid, current, false))
        {
            coords = current;
            return true;
        }

        for (var x = -radius; x <= radius; x++)
        {
            for (var y = -radius; y <= radius; y++)
            {
                if (x == 0 && y == 0)
                    continue;

                var candidate = new EntityCoordinates(gridUid, center + new Vector2i(x, y)).SnapToGrid(EntityManager);
                Consider(candidate, Math.Abs(x) + Math.Abs(y));
            }
        }

        if (best == null)
            return false;

        coords = best.Value;
        return true;
    }

    private bool IsAdjacentToWebbedTile(EntityCoordinates coords)
    {
        foreach (var offset in NeighborOffsets)
        {
            if (IsTileWebbed(coords.Offset(offset)))
                return true;
        }

        return false;
    }

    private bool TryGetGridData(
        EntityCoordinates origin,
        out EntityUid gridUid,
        out MapGridComponent grid,
        out Vector2i center)
    {
        gridUid = EntityUid.Invalid;
        grid = default!;
        center = default;

        origin = origin.SnapToGrid(EntityManager);

        if (_transform.GetGrid(origin) is not { } gridEnt)
            return false;

        if (!TryComp(gridEnt, out MapGridComponent? gridComp))
            return false;

        grid = gridComp;
        gridUid = gridEnt;

        center = _map.TileIndicesFor(gridUid, grid, _transform.ToMapCoordinates(origin));
        return true;
    }

    public bool IsTileWebbed(EntityCoordinates coords)
    {
        foreach (var entity in _lookup.GetEntitiesIntersecting(coords))
        {
            if (HasComp<WebComponent>(entity))
                return true;
        }

        return false;
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

        if (!CanSpinWebAt(args.Performer, coords, true))
            return;

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

    private bool CanSpinWebAt(EntityUid uid, EntityCoordinates coords, bool showPopup)
    {
        if (!HasComp<MapGridComponent>(_transform.GetGrid(coords)))
        {
            if (showPopup)
            {
                _popupSystem.PopupEntity(Loc.GetString("action-name-spin-web-space"),
                    uid,
                    uid,
                    Shared.Popups.PopupType.MediumCaution);
            }

            return false;
        }

        foreach (var entity in _lookup.GetEntitiesIntersecting(coords))
        {
            PhysicsComponent? physics = null;
            if (HasComp<WebComponent>(entity) ||
                (Resolve(entity, ref physics, false) &&
                 (physics.CollisionLayer & (int) CollisionGroup.Impassable) != 0 &&
                 !(TryComp<DoorComponent>(entity, out var door) &&
                   door.State != DoorState.Closed)))
            {
                if (showPopup)
                {
                    _popupSystem.PopupEntity(Loc.GetString("action-name-spin-web-blocked"),
                        uid,
                        uid,
                        Shared.Popups.PopupType.MediumCaution);
                }

                return false;
            }
        }

        return true;
    }

    private readonly EntProtoId ArachneWeb = "ArachneWeb";

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
