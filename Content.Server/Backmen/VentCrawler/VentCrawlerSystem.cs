using System.Numerics;
using Content.Server._White.Headcrab;
using Content.Server.Atmos.Piping.Components;
using Content.Server.NodeContainer;
using Content.Server.NodeContainer.EntitySystems;
using Content.Server.NodeContainer.Nodes;
using Content.Server.Popups;
using Content.Server.StationEvents.Components;
using Content.Shared.Backmen.Flesh;
using Content.Shared.Backmen.VentCrawler;
using Content.Shared.Eye;
using Content.Shared.Interaction.Events;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Events;
using Content.Shared.Movement.Systems;
using Content.Shared.NodeContainer;
using Content.Shared.Popups;
using Content.Shared.Tools.Components;
using Content.Shared.Tools.Systems;
using Content.Shared.Verbs;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server.Backmen.VentCrawler;

public sealed class VentCrawlerSystem : SharedVentCrawlerSystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly NodeContainerSystem _nodeContainer = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedVisibilitySystem _visibility = default!;
    [Dependency] private readonly WeldableSystem _weldable = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _movementSpeed = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;

    private EntityQuery<NodeContainerComponent> _nodeQuery;
    private EntityQuery<TransformComponent> _xformQuery;
    private EntityQuery<WeldableComponent> _weldableQuery;

    public override void Initialize()
    {
        base.Initialize();

        _nodeQuery = GetEntityQuery<NodeContainerComponent>();
        _xformQuery = GetEntityQuery<TransformComponent>();
        _weldableQuery = GetEntityQuery<WeldableComponent>();

        SubscribeLocalEvent<VentCrawlingComponent, GetVerbsEvent<InteractionVerb>>(OnGetExitVerbs);
        SubscribeLocalEvent<VentCrawlingComponent, MoveInputEvent>(OnMoveInput);
        SubscribeLocalEvent<VentCrawlingComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshSpeed);
        SubscribeLocalEvent<VentCrawlingComponent, GetVisMaskEvent>(OnGetVisMask);
        SubscribeLocalEvent<VentCrawlingComponent, GettingAttackedAttemptEvent>(OnAttacked);
        SubscribeLocalEvent<VentCrawlingComponent, ComponentStartup>(OnCrawlingStartup);
        SubscribeLocalEvent<VentCrawlingComponent, ComponentShutdown>(OnCrawlingShutdown);

        SubscribeLocalEvent<AtmosUnsafeUnanchorComponent, EntityTerminatingEvent>(OnPipeTerminating);

        SubscribeLocalEvent<VentCritterSpawnLocationComponent, GetVerbsEvent<InteractionVerb>>(OnVentGetVerbs);
    }

    private void OnVentGetVerbs(EntityUid uid, VentCritterSpawnLocationComponent component, GetVerbsEvent<InteractionVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        if (!TryComp(args.User, out VentCrawlerComponent? crawler))
            return;

        if (HasComp<VentCrawlingComponent>(args.User))
            return;

        if (!CanEnterVent(args.User, uid, crawler, out _))
            return;

        var user = args.User;
        var vent = uid;

        args.Verbs.Add(new InteractionVerb
        {
            Text = Loc.GetString("vent-crawler-verb-enter"),
            Act = () => TryEnterVent(user, vent),
        });
    }

    private void OnGetExitVerbs(EntityUid uid, VentCrawlingComponent component, GetVerbsEvent<InteractionVerb> args)
    {
        if (args.User != uid || !args.CanInteract)
            return;

        if (!IsOnVentTile(uid))
            return;

        args.Verbs.Add(new InteractionVerb
        {
            Text = Loc.GetString("vent-crawler-verb-exit"),
            Act = () => TryExitVent(uid),
        });
    }

    private void OnMoveInput(EntityUid uid, VentCrawlingComponent component, ref MoveInputEvent args)
    {
        if (args.Entity.Owner != uid || component.IsStepping)
            return;

        var pressed = args.Entity.Comp.HeldMoveButtons & ~args.OldMovement & MoveButtons.AnyDirection;
        if (pressed == MoveButtons.None)
            return;

        var dir = GetDirection(pressed);
        if (dir == null)
            return;

        TryStep(uid, dir.Value, component);
    }

    private void OnRefreshSpeed(EntityUid uid, VentCrawlingComponent component, RefreshMovementSpeedModifiersEvent args)
    {
        args.ModifySpeed(0f, 0f);
    }

    private void OnGetVisMask(EntityUid uid, VentCrawlingComponent component, ref GetVisMaskEvent args)
    {
        args.VisibilityMask |= (int) VisibilityFlags.Subfloor;
    }

    private void OnAttacked(EntityUid uid, VentCrawlingComponent component, ref GettingAttackedAttemptEvent args)
    {
        args.Cancelled = true;
    }

    private void OnCrawlingStartup(EntityUid uid, VentCrawlingComponent component, ComponentStartup args)
    {
        ApplySubfloorVisibility(uid);
        _movementSpeed.RefreshMovementSpeedModifiers(uid);

        if (TryComp(uid, out PhysicsComponent? physics))
        {
            _physics.SetCanCollide(uid, false, body: physics);
        }
    }

    private void OnCrawlingShutdown(EntityUid uid, VentCrawlingComponent component, ComponentShutdown args)
    {
        if (TerminatingOrDeleted(uid))
            return;

        RestoreVisibility(uid);
        _movementSpeed.RefreshMovementSpeedModifiers(uid);

        if (TryComp(uid, out PhysicsComponent? physics))
        {
            _physics.SetCanCollide(uid, true, body: physics);
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<VentCrawlingComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var crawling, out var xform))
        {
            if (!IsValidCrawlPipe(crawling.CurrentPipe))
            {
                ForceExitVent(uid, pipeBroken: true);
                continue;
            }

            if (!crawling.IsStepping)
                continue;

            crawling.StepTimeLeft -= frameTime;

            var progress = crawling.StepStartingTime <= 0
                ? 1f
                : 1f - crawling.StepTimeLeft / crawling.StepStartingTime;

            var origin = _transform.ToMapCoordinates(crawling.StepOrigin);
            var destination = _transform.ToMapCoordinates(crawling.StepDestination);
            var position = Vector2.Lerp(origin.Position, destination.Position, progress);
            _transform.SetMapCoordinates(uid, new MapCoordinates(position, origin.MapId));

            if (crawling.StepTimeLeft > 0)
                continue;

            crawling.IsStepping = false;
            Dirty(uid, crawling);
            _transform.SetCoordinates(uid, crawling.StepDestination);

            if (TryComp(uid, out VentCrawlerComponent? crawler))
                PlayVentSound(crawler.StepSound, uid);

            if (!IsValidCrawlPipe(crawling.CurrentPipe))
                ForceExitVent(uid, pipeBroken: true);
        }
    }

    public bool TryEnterVent(EntityUid user, EntityUid vent, VentCrawlerComponent? crawler = null)
    {
        if (!Resolve(user, ref crawler, false))
            return false;

        if (!CanEnterVent(user, vent, crawler, out var reason))
        {
            if (reason != null)
                _popup.PopupEntity(reason, user, user);

            return false;
        }

        if (!TryGetPipeNode(vent, out var pipeNode))
            return false;

        var ventXform = Transform(vent);
        _transform.SetCoordinates(user, ventXform.Coordinates);

        var crawling = EnsureComp<VentCrawlingComponent>(user);
        crawling.CurrentPipe = vent;
        crawling.IsStepping = false;
        Dirty(user, crawling);

        crawler.NextEnterAt = _timing.CurTime + crawler.EnterCooldown;
        _popup.PopupEntity(Loc.GetString("vent-crawler-entered"), user, user);
        PlayVentSound(crawler.EnterSound, user);

        return true;
    }

    public bool TryExitVent(EntityUid user, VentCrawlingComponent? crawling = null)
    {
        if (!Resolve(user, ref crawling, false))
            return false;

        if (!IsOnVentTile(user))
        {
            _popup.PopupEntity(Loc.GetString("vent-crawler-exit-fail"), user, user);
            return false;
        }

        if (_weldableQuery.TryComp(GetVentOnTile(user), out _) && _weldable.IsWelded(GetVentOnTile(user)))
        {
            _popup.PopupEntity(Loc.GetString("vent-crawler-welded"), user, user);
            return false;
        }

        RemComp<VentCrawlingComponent>(user);
        _popup.PopupEntity(Loc.GetString("vent-crawler-exited"), user, user);

        if (TryComp(user, out VentCrawlerComponent? crawler))
            PlayVentSound(crawler.ExitSound, user);

        return true;
    }

    public bool ForceExitVent(
        EntityUid user,
        EntityCoordinates? exitCoords = null,
        VentCrawlingComponent? crawling = null,
        bool pipeBroken = false)
    {
        if (!Resolve(user, ref crawling, false))
            return false;

        if (crawling.IsStepping)
        {
            crawling.IsStepping = false;
            Dirty(user, crawling);
        }

        if (exitCoords != null)
            _transform.SetCoordinates(user, exitCoords.Value);

        RemComp<VentCrawlingComponent>(user);

        var message = pipeBroken ? "vent-crawler-pipe-broken" : "vent-crawler-exited";
        _popup.PopupEntity(Loc.GetString(message), user, user);

        if (TryComp(user, out VentCrawlerComponent? crawler))
            PlayVentSound(crawler.ExitSound, user);

        return true;
    }

    public bool TryStep(EntityUid user, Direction direction, VentCrawlingComponent? crawling = null, VentCrawlerComponent? crawler = null)
    {
        if (!Resolve(user, ref crawling, false))
            return false;

        if (crawling.IsStepping)
            return false;

        Resolve(user, ref crawler, false);

        if (!TryGetNextPipe(crawling.CurrentPipe, direction, out var nextPipe))
        {
            if (TryGetTargetPipeTile(crawling.CurrentPipe, direction, out var gridUid, out var grid, out var targetPos) &&
                HasBrokenOrMissingPipeAt(gridUid, grid, targetPos))
            {
                var targetCoords = _map.GridTileToLocal(gridUid, grid, targetPos);
                return ForceExitVent(user, targetCoords, crawling, pipeBroken: true);
            }

            _popup.PopupEntity(Loc.GetString("vent-crawler-dead-end"), user, user);
            return false;
        }

        var nextXform = Transform(nextPipe);
        var userXform = Transform(user);

        crawling.StepOrigin = userXform.Coordinates;
        crawling.StepDestination = nextXform.Coordinates;
        crawling.StepStartingTime = crawler?.StepDelay ?? 0.35f;
        crawling.StepTimeLeft = crawling.StepStartingTime;
        crawling.IsStepping = true;
        crawling.CurrentPipe = nextPipe;
        Dirty(user, crawling);

        return true;
    }

    private void PlayVentSound(SoundSpecifier sound, EntityUid source)
    {
        _audio.PlayPvs(sound, source);
    }

    private void OnPipeTerminating(EntityUid uid, AtmosUnsafeUnanchorComponent component, EntityTerminatingEvent args)
    {
        EjectCrawlersOnPipe(uid);
    }

    private void EjectCrawlersOnPipe(EntityUid pipe)
    {
        var query = EntityQueryEnumerator<VentCrawlingComponent>();
        while (query.MoveNext(out var user, out var crawling))
        {
            if (crawling.CurrentPipe != pipe)
                continue;

            ForceExitVent(user, pipeBroken: true);
        }
    }

    public bool IsValidCrawlPipe(EntityUid uid)
    {
        if (!uid.IsValid() || TerminatingOrDeleted(uid))
            return false;

        if (MetaData(uid).EntityPrototype is { ID: var protoId } && protoId == GasPipeBrokenPrototype)
            return false;

        return TryGetPipeNode(uid, out _);
    }

    public bool CanEnterVent(EntityUid user, EntityUid vent, VentCrawlerComponent crawler, out string? reason)
    {
        reason = null;

        if (HasComp<VentCrawlingComponent>(user))
        {
            reason = Loc.GetString("vent-crawler-already-inside");
            return false;
        }

        if (_timing.CurTime < crawler.NextEnterAt)
        {
            reason = Loc.GetString("vent-crawler-cooldown");
            return false;
        }

        if (TryComp<FleshWormComponent>(user, out var worm) && worm.EquipedOn is { Valid: true })
        {
            reason = Loc.GetString("vent-crawler-equipped");
            return false;
        }

        if (TryComp<HeadcrabComponent>(user, out var headcrab) &&
            headcrab.EquippedOn is { Valid: true })
        {
            reason = Loc.GetString("vent-crawler-equipped");
            return false;
        }

        if (!IsGasVent(vent))
        {
            reason = Loc.GetString("vent-crawler-not-vent");
            return false;
        }

        if (_weldableQuery.HasComponent(vent) && _weldable.IsWelded(vent))
        {
            reason = Loc.GetString("vent-crawler-welded");
            return false;
        }

        if (!IsValidCrawlPipe(vent))
        {
            reason = Loc.GetString("vent-crawler-no-pipe");
            return false;
        }

        var userPos = _transform.GetMapCoordinates(user);
        var ventPos = _transform.GetMapCoordinates(vent);

        if (userPos.MapId != ventPos.MapId ||
            (userPos.Position - ventPos.Position).LengthSquared() > crawler.EnterRange * crawler.EnterRange)
        {
            reason = Loc.GetString("vent-crawler-too-far");
            return false;
        }

        return true;
    }

    public bool IsOnVentTile(EntityUid user)
    {
        return GetVentOnTile(user) is { Valid: true };
    }

    private EntityUid GetVentOnTile(EntityUid user)
    {
        var userXform = Transform(user);

        if (userXform.GridUid is not { } gridUid || !TryComp<MapGridComponent>(gridUid, out var grid))
            return EntityUid.Invalid;

        var indices = _map.TileIndicesFor(gridUid, grid, userXform.Coordinates);

        foreach (var entity in _map.GetAnchoredEntities(gridUid, grid, indices))
        {
            if (IsGasVent(entity))
                return entity;
        }

        return EntityUid.Invalid;
    }

    private bool TryGetNextPipe(EntityUid currentPipe, Direction direction, out EntityUid nextPipe)
    {
        nextPipe = EntityUid.Invalid;

        if (!TryGetPipeNode(currentPipe, out var currentNode) || currentNode == null)
            return false;

        var currentXform = _xformQuery.GetComponent(currentPipe);

        if (currentXform.GridUid is not { } gridUid || !TryComp<MapGridComponent>(gridUid, out var grid))
            return false;

        var currentPos = _map.TileIndicesFor(gridUid, grid, currentXform.Coordinates);
        var targetPos = currentPos + direction.ToIntVec();

        var gridEnt = (gridUid, grid);

        foreach (var node in currentNode.GetReachableNodes(
                     (currentPipe, currentXform),
                     _nodeQuery,
                     _xformQuery,
                     gridEnt,
                     EntityManager))
        {
            if (node is not PipeNode)
                continue;

            var ownerXform = _xformQuery.GetComponent(node.Owner);
            var ownerPos = _map.TileIndicesFor(gridUid, grid, ownerXform.Coordinates);

            if (ownerPos != targetPos)
                continue;

            nextPipe = node.Owner;
            return true;
        }

        foreach (var entity in _map.GetAnchoredEntities(gridUid, grid, targetPos))
        {
            if (!IsGasVent(entity) || !IsValidCrawlPipe(entity))
                continue;

            nextPipe = entity;
            return true;
        }

        return false;
    }

    private bool TryGetTargetPipeTile(
        EntityUid currentPipe,
        Direction direction,
        out EntityUid gridUid,
        out MapGridComponent grid,
        out Vector2i targetPos)
    {
        gridUid = EntityUid.Invalid;
        grid = default!;
        targetPos = default;

        var currentXform = _xformQuery.GetComponent(currentPipe);

        if (currentXform.GridUid is not { } foundGridUid || !TryComp(foundGridUid, out MapGridComponent? gridComp))
            return false;

        grid = gridComp;

        gridUid = foundGridUid;
        var currentPos = _map.TileIndicesFor(gridUid, grid, currentXform.Coordinates);
        targetPos = currentPos + direction.ToIntVec();
        return true;
    }

    private bool HasBrokenOrMissingPipeAt(EntityUid gridUid, MapGridComponent grid, Vector2i tile)
    {
        var hasCrawlPipe = false;

        foreach (var entity in _map.GetAnchoredEntities(gridUid, grid, tile))
        {
            if (MetaData(entity).EntityPrototype is { ID: var protoId } && protoId == GasPipeBrokenPrototype)
                return true;

            if (IsValidCrawlPipe(entity))
                hasCrawlPipe = true;
        }

        return !hasCrawlPipe;
    }

    private bool TryGetPipeNode(EntityUid uid, out PipeNode? pipeNode)
    {
        pipeNode = null;

        if (!_nodeQuery.TryComp(uid, out var container))
            return false;

        if (!_nodeContainer.TryGetNode(container, "pipe", out PipeNode? node))
            return false;

        pipeNode = node;
        return true;
    }

    private void ApplySubfloorVisibility(EntityUid uid)
    {
        var visibility = EnsureComp<VisibilityComponent>(uid);
        _visibility.RemoveLayer((uid, visibility), (int) VisibilityFlags.Normal, false);
        _visibility.AddLayer((uid, visibility), (int) VisibilityFlags.Subfloor, false);
        _visibility.RefreshVisibility(uid);
    }

    private void RestoreVisibility(EntityUid uid)
    {
        if (!TryComp<VisibilityComponent>(uid, out var visibility))
            return;

        _visibility.RemoveLayer((uid, visibility), (int) VisibilityFlags.Subfloor, false);
        _visibility.AddLayer((uid, visibility), (int) VisibilityFlags.Normal, false);
        _visibility.RefreshVisibility(uid);
    }

    private static Direction? GetDirection(MoveButtons pressed)
    {
        if ((pressed & MoveButtons.Up) != 0)
            return Direction.North;

        if ((pressed & MoveButtons.Down) != 0)
            return Direction.South;

        if ((pressed & MoveButtons.Left) != 0)
            return Direction.West;

        if ((pressed & MoveButtons.Right) != 0)
            return Direction.East;

        return null;
    }
}
