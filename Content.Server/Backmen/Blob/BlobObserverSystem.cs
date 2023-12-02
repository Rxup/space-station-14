using System.Linq;
using System.Numerics;
using Content.Server.Actions;
using Content.Server.Atmos.Components;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Backmen.GameTicking.Rules.Components;
using Content.Server.Chat.Managers;
using Content.Server.Destructible;
using Content.Server.Emp;
using Content.Server.Explosion.EntitySystems;
using Content.Server.Mind;
using Content.Server.Roles;
using Content.Shared.ActionBlocker;
using Content.Shared.Alert;
using Content.Shared.Backmen.Blob;
using Content.Shared.Coordinates.Helpers;
using Content.Shared.Damage;
using Content.Shared.Interaction;
using Content.Shared.Item;
using Content.Shared.Mind;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Content.Shared.SubFloor;
using Robust.Server.Audio;
using Robust.Server.GameObjects;
using Robust.Server.Player;
using Robust.Shared.Audio;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Backmen.Blob;

public sealed class BlobObserverSystem : SharedBlobObserverSystem
{
    [Dependency] private readonly ActionsSystem _action = default!;
    [Dependency] private readonly IMapManager _map = default!;
    [Dependency] private readonly DamageableSystem _damageableSystem = default!;
    [Dependency] private readonly BlobCoreSystem _blobCoreSystem = default!;
    [Dependency] private readonly AudioSystem _audioSystem = default!;
    [Dependency] private readonly ITileDefinitionManager _tileDefinitionManager = default!;
    [Dependency] private readonly IGameTiming _gameTiming = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly ActionBlockerSystem _blocker = default!;
    [Dependency] private readonly UserInterfaceSystem _uiSystem = default!;
    [Dependency] private readonly ExplosionSystem _explosionSystem = default!;
    [Dependency] private readonly FlammableSystem _flammable = default!;
    [Dependency] private readonly EmpSystem _empSystem = default!;
    [Dependency] private readonly MindSystem _mindSystem = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly RoleSystem _roleSystem = default!;
    [Dependency] private readonly AlertsSystem _alerts = default!;
    [Dependency] private readonly IChatManager _chatManager = default!;
    [Dependency] private readonly ISharedPlayerManager _actorSystem = default!;
    [Dependency] private readonly ViewSubscriberSystem _viewSubscriberSystem = default!;


    [Dependency] private ILogManager _logMan = default!;
    private ISawmill _logger = default!;
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BlobCoreComponent, CreateBlobObserverEvent>(OnCreateBlobObserver);

        SubscribeLocalEvent<BlobObserverComponent, PlayerAttachedEvent>(OnPlayerAttached, before: new []{ typeof(ActionsSystem) });
        SubscribeLocalEvent<BlobObserverComponent, PlayerDetachedEvent>(OnPlayerDetached, before: new []{ typeof(ActionsSystem) });

        SubscribeLocalEvent<BlobCoreComponent, BlobCreateFactoryActionEvent>(OnCreateFactory);
        SubscribeLocalEvent<BlobCoreComponent, BlobCreateResourceActionEvent>(OnCreateResource);
        SubscribeLocalEvent<BlobCoreComponent, BlobCreateNodeActionEvent>(OnCreateNode);
        SubscribeLocalEvent<BlobCoreComponent, BlobCreateBlobbernautActionEvent>(OnCreateBlobbernaut);
        SubscribeLocalEvent<BlobCoreComponent, BlobToCoreActionEvent>(OnBlobToCore);
        SubscribeLocalEvent<BlobCoreComponent, BlobToNodeActionEvent>(OnBlobToNode);
        SubscribeLocalEvent<BlobCoreComponent, BlobHelpActionEvent>(OnBlobHelp);
        SubscribeLocalEvent<BlobCoreComponent, BlobSwapChemActionEvent>(OnBlobSwapChem);
        SubscribeLocalEvent<BlobObserverComponent, InteractNoHandEvent>(OnInteract);
        SubscribeLocalEvent<BlobCoreComponent, BlobSwapCoreActionEvent>(OnSwapCore);
        SubscribeLocalEvent<BlobCoreComponent, BlobSplitCoreActionEvent>(OnSplitCore);

        SubscribeLocalEvent<BlobObserverComponent, MoveEvent>(OnMoveEvent);
        SubscribeLocalEvent<BlobObserverComponent, BlobChemSwapPrototypeSelectedMessage>(OnChemSelected);


        _logger = _logMan.GetSawmill("blob.core");
    }

    private void SendBlobBriefing(EntityUid mind)
    {
        if (_mindSystem.TryGetSession(mind, out var session))
        {
            _chatManager.DispatchServerMessage(session, Loc.GetString("blob-role-greeting"));
        }
    }

    private void OnCreateBlobObserver(EntityUid blobCoreUid, BlobCoreComponent core, CreateBlobObserverEvent args)
    {
        var observer = Spawn(core.ObserverBlobPrototype, Transform(blobCoreUid).Coordinates);

        core.Observer = observer;

        if (!TryComp<BlobObserverComponent>(observer, out var blobObserverComponent))
        {
            args.Cancel();
            return;
        }

        blobObserverComponent.Core = blobCoreUid;


        var isNewMind = false;
        if (!_mindSystem.TryGetMind(blobCoreUid, out var mindId, out var mind))
        {
            if (
                !_playerManager.TryGetSessionById(args.UserId, out var playerSession) ||
                playerSession.AttachedEntity == null ||
                !_mindSystem.TryGetMind(playerSession.AttachedEntity.Value, out mindId, out mind))
            {
                mindId = _mindSystem.CreateMind(args.UserId, "Blob Player");
                mind = Comp<MindComponent>(mindId);
                isNewMind = true;
            }
        }

        //_mindSystem.SetUserId(mindId, args.UserId);
        if (!isNewMind)
        {
            /*var obj = mind.AllObjectives.ToArray();
            for (var i = 0; i < obj.Length; i++)
            {
                _mindSystem.TryRemoveObjective(mindId, mind, i);
            }
            _metaDataSystem.SetEntityName(observer,"Blob player");
            if (_roleSystem.MindHasRole<JobComponent>(mindId))
            {

            }*/
            var name = mind.Session?.Name ?? "???";
            _mindSystem.WipeMind(mindId, mind);
            mindId = _mindSystem.CreateMind(args.UserId, $"Blob Player ({name})");
            mind = Comp<MindComponent>(mindId);
            isNewMind = true;
        }

        _roleSystem.MindAddRole(mindId, new BlobRoleComponent{ PrototypeId = core.AntagBlobPrototypeId });
        SendBlobBriefing(mindId);

        _alerts.ShowAlert(observer, AlertType.BlobHealth, (short) Math.Clamp(Math.Round(core.CoreBlobTotalHealth.Float() / 10f), 0, 20));

        var blobRule = EntityQuery<BlobRuleComponent>().FirstOrDefault();
        blobRule?.Blobs.Add((mindId,mind));

        _mindSystem.TryAddObjective(mindId, mind, "BlobCaptureObjective");

        _mindSystem.TransferTo(mindId, observer, true, mind: mind);
        if (_actorSystem.TryGetSessionById(args.UserId, out var session))
        {
            _actorSystem.SetAttachedEntity(session, observer, true);
        }

        UpdateUi(observer, core);

        //RemComp<ActionsComponent>(observer);
        /*
        if (isNewMind)
        {
            _mindSystem.TransferTo(mindId, observer, true, mind: mind);
        }
        Timer.Spawn(1_000, () =>
        {
            _mindSystem.TransferTo(mindId, null, true, mind: mind);

            Timer.Spawn(1_000, () =>
            {
                _mindSystem.TransferTo(mindId, observer, true, mind: mind);
                if (_actorSystem.TryGetActorFromUserId(args.UserId, out var session, out _))
                {
                    _actorSystem.Attach(observer, session, true);
                }
                UpdateUi(observer, blobObserverComponent);
            });
        });
        */
    }

    private void UpdateActions(ICommonSession playerSession, EntityUid uid, BlobObserverComponent? component = null)
    {
        if (!Resolve(uid, ref component))
        {
            return;
        }

        if (component.Core == null || TerminatingOrDeleted(component.Core.Value) || !TryComp<BlobCoreComponent>(component.Core.Value, out var coreComponent))
        {
            _logger.Error("Не возможно найти ядро для обсервера!");
            return;
        }

        _action.GrantActions(uid, new []
        {
            coreComponent.ActionHelpBlob!.Value,
            coreComponent.ActionSwapBlobChem!.Value,
            coreComponent.ActionTeleportBlobToCore!.Value,
            coreComponent.ActionTeleportBlobToNode!.Value,
            coreComponent.ActionCreateBlobFactory!.Value,
            coreComponent.ActionCreateBlobResource!.Value,
            coreComponent.ActionCreateBlobNode!.Value,
            coreComponent.ActionCreateBlobbernaut!.Value,
            coreComponent.ActionSplitBlobCore!.Value,
            coreComponent.ActionSwapBlobCore!.Value
        }, component.Core.Value);

        _viewSubscriberSystem.AddViewSubscriber(component.Core.Value, playerSession); // GrantActions require keep in pvs
    }

    private void OnPlayerAttached(EntityUid uid, BlobObserverComponent component, PlayerAttachedEvent args)
    {
        UpdateActions(args.Player, uid, component);
    }
    private void OnPlayerDetached(EntityUid uid, BlobObserverComponent component, PlayerDetachedEvent args)
    {
        if (component.Core.HasValue && !TerminatingOrDeleted(component.Core.Value))
        {
            _viewSubscriberSystem.RemoveViewSubscriber(component.Core.Value, args.Player);
        }
    }

    private void OnBlobSwapChem(EntityUid uid, BlobCoreComponent blobCoreComponent,
        BlobSwapChemActionEvent args)
    {
        if (!TryComp<BlobObserverComponent>(args.Performer, out var observerComponent))
            return;

        TryOpenUi(args.Performer, args.Performer, observerComponent);
        args.Handled = true;
    }

    private void OnChemSelected(EntityUid uid, BlobObserverComponent component, BlobChemSwapPrototypeSelectedMessage args)
    {
        if (component.Core == null || !TryComp<BlobCoreComponent>(component.Core.Value, out var blobCoreComponent))
            return;

        if (component.SelectedChemId == args.SelectedId)
            return;

        if (!_blobCoreSystem.TryUseAbility(uid, component.Core.Value, blobCoreComponent,
                blobCoreComponent.SwapChemCost))
            return;

        ChangeChem(uid, args.SelectedId, component);
    }

    private void ChangeChem(EntityUid uid, BlobChemType newChem, BlobObserverComponent component)
    {
        if (component.Core == null || !TryComp<BlobCoreComponent>(component.Core.Value, out var blobCoreComponent))
            return;
        component.SelectedChemId = newChem;
        _blobCoreSystem.ChangeChem(component.Core.Value, newChem, blobCoreComponent);

        _popup.PopupEntity(Loc.GetString("blob-spent-resource", ("point", blobCoreComponent.SwapChemCost)),
            uid,
            uid,
            PopupType.LargeCaution);

        UpdateUi(uid, blobCoreComponent);
    }

    private void TryOpenUi(EntityUid uid, EntityUid user, BlobObserverComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        if (!TryComp(user, out ActorComponent? actor))
            return;

        _uiSystem.TryToggleUi(uid, BlobChemSwapUiKey.Key, actor.PlayerSession);
    }

    public void UpdateUi(EntityUid uid, BlobCoreComponent blobCoreComponent)
    {
        if (!TryComp<BlobObserverComponent>(uid, out var observerComponent))
        {
            return;
        }
        var state = new BlobChemSwapBoundUserInterfaceState(blobCoreComponent.ChemСolors, observerComponent.SelectedChemId);

        _uiSystem.TrySetUiState(uid, BlobChemSwapUiKey.Key, state);
    }

    // TODO: This is very bad, but it is clearly better than invisible walls, let someone do better.
    private void OnMoveEvent(EntityUid uid, BlobObserverComponent observerComponent, ref MoveEvent args)
    {
        if (observerComponent.IsProcessingMoveEvent)
            return;

        observerComponent.IsProcessingMoveEvent = true;

        if (observerComponent.Core == null)
        {
            observerComponent.IsProcessingMoveEvent = false;
            return;
        }

        if (Deleted(observerComponent.Core.Value) ||
            !TryComp<TransformComponent>(observerComponent.Core.Value, out var xform))
        {
            return;
        }

        var corePos = xform.Coordinates;

        var (nearestEntityUid, nearestDistance) = CalculateNearestBlobTileDistance(args.NewPosition);

        if (nearestEntityUid == null)
            return;

        if (nearestDistance > 5f)
        {
            _transform.SetCoordinates(uid, corePos);

            observerComponent.IsProcessingMoveEvent = false;
            return;
        }

        if (nearestDistance > 3f)
        {
            observerComponent.CanMove = true;
            _blocker.UpdateCanMove(uid);
            var direction = (Transform(nearestEntityUid.Value).Coordinates.Position - args.NewPosition.Position);
            var newPosition = args.NewPosition.Offset(direction * 0.1f);

            _transform.SetCoordinates(uid, newPosition);
        }

        observerComponent.IsProcessingMoveEvent = false;
    }

    private (EntityUid? nearestEntityUid, float nearestDistance) CalculateNearestBlobTileDistance(EntityCoordinates position)
    {
        var nearestDistance = float.MaxValue;
        EntityUid? nearestEntityUid = null;

        foreach (var lookupUid in _lookup.GetEntitiesInRange(position, 5f))
        {
            if (!HasComp<BlobTileComponent>(lookupUid))
                continue;
            var tileCords = Transform(lookupUid).Coordinates;
            var distance = Vector2.Distance(position.Position, tileCords.Position);

            if (!(distance < nearestDistance))
                continue;
            nearestDistance = distance;
            nearestEntityUid = lookupUid;
        }

        return (nearestEntityUid, nearestDistance);
    }

    private void OnBlobHelp(EntityUid uid, BlobCoreComponent blobCoreComponent,
        BlobHelpActionEvent args)
    {
        _popup.PopupEntity(Loc.GetString("blob-help"), args.Performer, args.Performer, PopupType.Large);
        args.Handled = true;
    }

    private void OnSplitCore(EntityUid uid, BlobCoreComponent blobCoreComponent,
        BlobSplitCoreActionEvent args)
    {
        if (args.Handled)
            return;

        if (!blobCoreComponent.CanSplit)
        {
            _popup.PopupEntity(Loc.GetString("blob-cant-split"), args.Performer, args.Performer, PopupType.Large);
            return;
        }

        var gridUid = args.Target.GetGridUid(EntityManager);

        if (!_map.TryGetGrid(gridUid, out var grid))
        {
            return;
        }

        var centerTile = grid.GetLocalTilesIntersecting(
            new Box2(args.Target.Position, args.Target.Position)).ToArray();

        EntityUid? blobTile = null;

        foreach (var tileref in centerTile)
        {
            foreach (var ent in grid.GetAnchoredEntities(tileref.GridIndices))
            {
                if (!TryComp<BlobTileComponent>(ent, out var blobTileComponent))
                    continue;
                blobTile = ent;
                break;
            }
        }

        if (blobTile == null || !TryComp<BlobNodeComponent>(blobTile, out var blobNodeComponent))
        {
            _popup.PopupEntity(Loc.GetString("blob-target-node-blob-invalid"), args.Performer, args.Performer, PopupType.Large);
            args.Handled = true;
            return;
        }

        if (!_blobCoreSystem.TryUseAbility(args.Performer, uid, blobCoreComponent,
                blobCoreComponent.SplitCoreCost))
        {
            args.Handled = true;
            return;
        }

        QueueDel(blobTile.Value);
        var newCore = EntityManager.SpawnEntity(blobCoreComponent.CoreBlobTile, args.Target);
        blobCoreComponent.CanSplit = false;
        if (TryComp<BlobCoreComponent>(newCore, out var newBlobCoreComponent))
            newBlobCoreComponent.CanSplit = false;

        args.Handled = true;
    }


    private void OnSwapCore(EntityUid uid, BlobCoreComponent blobCoreComponent,
        BlobSwapCoreActionEvent args)
    {
        if (args.Handled)
            return;

        var gridUid = args.Target.GetGridUid(EntityManager);

        if (!_map.TryGetGrid(gridUid, out var grid))
        {
            return;
        }

        var centerTile = grid.GetLocalTilesIntersecting(
            new Box2(args.Target.Position, args.Target.Position)).ToArray();

        EntityUid? blobTile = null;

        foreach (var tileRef in centerTile)
        {
            foreach (var ent in grid.GetAnchoredEntities(tileRef.GridIndices))
            {
                if (!TryComp<BlobTileComponent>(ent, out var blobTileComponent))
                    continue;
                blobTile = ent;
                break;
            }
        }

        if (blobTile == null || !TryComp<BlobNodeComponent>(blobTile, out var blobNodeComponent))
        {
            _popup.PopupEntity(Loc.GetString("blob-target-node-blob-invalid"), args.Performer, args.Performer, PopupType.Large);
            args.Handled = true;
            return;
        }

        if (!_blobCoreSystem.TryUseAbility(args.Performer, uid, blobCoreComponent,
                blobCoreComponent.SwapCoreCost))
        {
            args.Handled = true;
            return;
        }

        var nodePos = Transform(blobTile.Value).Coordinates;
        var corePos = Transform(uid).Coordinates;
        _transform.SetCoordinates(uid, nodePos.SnapToGrid());
        _transform.SetCoordinates(blobTile.Value, corePos.SnapToGrid());
        var xformCore = Transform(uid);
        if (!xformCore.Anchored)
        {
            _transform.AnchorEntity(uid, xformCore);
        }
        var xformNode = Transform(blobTile.Value);
        if (!xformNode.Anchored)
        {
            _transform.AnchorEntity(blobTile.Value, xformNode);
        }
        args.Handled = true;
    }

    private void OnBlobToNode(EntityUid uid, BlobCoreComponent blobCoreComponent,
        BlobToNodeActionEvent args)
    {
        if (args.Handled)
            return;

        var blobNodes = new List<EntityUid>();

        var blobNodeQuery = EntityQueryEnumerator<BlobNodeComponent, BlobTileComponent>();
        while (blobNodeQuery.MoveNext(out var ent, out var node, out var tile))
        {
            if (tile.Core == uid && !HasComp<BlobCoreComponent>(ent))
                blobNodes.Add(ent);
        }

        if (blobNodes.Count == 0)
        {
            _popup.PopupEntity(Loc.GetString("blob-not-have-nodes"), args.Performer, args.Performer, PopupType.Large);
            args.Handled = true;
            return;
        }

        _transform.SetCoordinates(args.Performer, Transform(_random.Pick(blobNodes)).Coordinates);
        args.Handled = true;
    }

    private void OnCreateBlobbernaut(EntityUid uid, BlobCoreComponent blobCoreComponent,
        BlobCreateBlobbernautActionEvent args)
    {
        if (args.Handled)
            return;

        var gridUid = args.Target.GetGridUid(EntityManager);

        if (!_map.TryGetGrid(gridUid, out var grid))
        {
            return;
        }

        var centerTile = grid.GetLocalTilesIntersecting(
            new Box2(args.Target.Position, args.Target.Position)).ToArray();

        EntityUid? blobTile = null;

        foreach (var tileRef in centerTile)
        {
            foreach (var ent in grid.GetAnchoredEntities(tileRef.GridIndices))
            {
                if (!HasComp<BlobFactoryComponent>(ent))
                    continue;
                blobTile = ent;
                break;
            }
        }

        if (blobTile == null || !TryComp<BlobFactoryComponent>(blobTile, out var blobFactoryComponent))
        {
            _popup.PopupEntity(Loc.GetString("blob-target-factory-blob-invalid"), args.Performer, args.Performer, PopupType.LargeCaution);
            return;
        }

        if (blobFactoryComponent.Blobbernaut != null)
        {
            _popup.PopupEntity(Loc.GetString("blob-target-already-produce-blobbernaut"), args.Performer, args.Performer, PopupType.LargeCaution);
            return;
        }

        if (!_blobCoreSystem.TryUseAbility(args.Performer, uid, blobCoreComponent, blobCoreComponent.BlobbernautCost))
            return;

        var ev = new ProduceBlobbernautEvent();
        RaiseLocalEvent(blobTile.Value, ev);

        _popup.PopupEntity(Loc.GetString("blob-spent-resource", ("point", blobCoreComponent.BlobbernautCost)),
            blobTile.Value,
            uid,
            PopupType.LargeCaution);

        args.Handled = true;
    }

    private void OnBlobToCore(EntityUid uid, BlobCoreComponent blobCoreComponent,
        BlobToCoreActionEvent args)
    {
        if (args.Handled)
            return;

        _transform.SetCoordinates(args.Performer, Transform(uid).Coordinates);
    }

    private void OnCreateNode(EntityUid uid, BlobCoreComponent blobCoreComponent,
        BlobCreateNodeActionEvent args)
    {
        if (args.Handled)
            return;

        var gridUid = args.Target.GetGridUid(EntityManager);

        if (!_map.TryGetGrid(gridUid, out var grid))
        {
            return;
        }

        var centerTile = grid.GetLocalTilesIntersecting(
            new Box2(args.Target.Position, args.Target.Position)).ToArray();

        var blobTileType = BlobTileType.None;
        EntityUid? blobTile = null;

        foreach (var tileRef in centerTile)
        {
            foreach (var ent in grid.GetAnchoredEntities(tileRef.GridIndices))
            {
                if (!TryComp<BlobTileComponent>(ent, out var blobTileComponent))
                    continue;
                blobTileType = blobTileComponent.BlobTileType;
                blobTile = ent;
                break;
            }
        }

        if (blobTileType is not BlobTileType.Normal ||
            blobTile == null)
        {
            _popup.PopupEntity(Loc.GetString("blob-target-normal-blob-invalid"), uid, uid, PopupType.Large);
            return;
        }

        var xform = Transform(blobTile.Value);

        var localPos = xform.Coordinates.Position;

        var radius = blobCoreComponent.NodeRadiusLimit;

        var innerTiles = grid.GetLocalTilesIntersecting(
            new Box2(localPos + new Vector2(-radius, -radius), localPos + new Vector2(radius, radius)), false).ToArray();

        foreach (var tileRef in innerTiles)
        {
            foreach (var ent in grid.GetAnchoredEntities(tileRef.GridIndices))
            {
                if (!HasComp<BlobNodeComponent>(ent))
                    continue;
                _popup.PopupEntity(Loc.GetString("blob-target-close-to-node"), uid, uid, PopupType.Large);
                return;
            }
        }

        if (!_blobCoreSystem.TryUseAbility(args.Performer, uid, blobCoreComponent, blobCoreComponent.NodeBlobCost))
            return;

        if (!_blobCoreSystem.TransformBlobTile(blobTile.Value,
                uid,
                blobCoreComponent.NodeBlobTile,
                args.Target,
                blobCoreComponent,
                transformCost: blobCoreComponent.NodeBlobCost))
            return;

        args.Handled = true;
    }

    private void OnCreateResource(EntityUid uid, BlobCoreComponent blobCoreComponent,
        BlobCreateResourceActionEvent args)
    {
        if (args.Handled)
            return;

        var gridUid = args.Target.GetGridUid(EntityManager);

        if (!_map.TryGetGrid(gridUid, out var grid))
        {
            return;
        }

        var centerTile = grid.GetLocalTilesIntersecting(
            new Box2(args.Target.Position, args.Target.Position)).ToArray();

        var blobTileType = BlobTileType.None;
        EntityUid? blobTile = null;

        foreach (var tileref in centerTile)
        {
            foreach (var ent in grid.GetAnchoredEntities(tileref.GridIndices))
            {
                if (!TryComp<BlobTileComponent>(ent, out var blobTileComponent))
                    continue;
                blobTileType = blobTileComponent.BlobTileType;
                blobTile = ent;
                break;
            }
        }

        if (blobTileType is not BlobTileType.Normal ||
            blobTile == null)
        {
            _popup.PopupEntity(Loc.GetString("blob-target-normal-blob-invalid"), uid, uid, PopupType.Large);
            return;
        }

        var xform = Transform(blobTile.Value);

        var localPos = xform.Coordinates.Position;

        var radius = blobCoreComponent.ResourceRadiusLimit;

        var innerTiles = grid.GetLocalTilesIntersecting(
            new Box2(localPos + new Vector2(-radius, -radius), localPos + new Vector2(radius, radius)), false).ToArray();

        foreach (var tileRef in innerTiles)
        {
            foreach (var ent in grid.GetAnchoredEntities(tileRef.GridIndices))
            {
                if (!HasComp<BlobResourceComponent>(ent) || HasComp<BlobCoreComponent>(ent))
                    continue;
                _popup.PopupEntity(Loc.GetString("blob-target-close-to-resource"), uid, uid, PopupType.Large);
                return;
            }
        }

        if (!_blobCoreSystem.CheckNearNode(args.Performer, xform.Coordinates, grid, blobCoreComponent))
            return;

        if (!_blobCoreSystem.TryUseAbility(args.Performer,
                uid,
                blobCoreComponent,
                blobCoreComponent.ResourceBlobCost))
            return;

        if (!_blobCoreSystem.TransformBlobTile(blobTile.Value,
                uid,
                blobCoreComponent.ResourceBlobTile,
                args.Target,
                blobCoreComponent,
                transformCost: blobCoreComponent.ResourceBlobCost))
            return;

        args.Handled = true;
    }

    private void OnInteract(EntityUid uid, BlobObserverComponent observerComponent, InteractNoHandEvent args)
    {
        if (args.Target == args.User)
            return;

        if (observerComponent.Core == null ||
            !TryComp<BlobCoreComponent>(observerComponent.Core.Value, out var blobCoreComponent))
            return;

        var location = args.ClickLocation;
        if (!location.IsValid(EntityManager))
            return;

        var gridId = location.GetGridUid(EntityManager);
        if (!HasComp<MapGridComponent>(gridId))
        {
            location = location.AlignWithClosestGridTile();
            gridId = location.GetGridUid(EntityManager);
            if (!HasComp<MapGridComponent>(gridId))
                return;
        }

        if (!_map.TryGetGrid(gridId, out var grid))
        {
            return;
        }

        if (args.Target != null &&
            !HasComp<BlobTileComponent>(args.Target.Value) &&
            !HasComp<BlobMobComponent>(args.Target.Value))
        {
            var target = args.Target.Value;

            // Check if the target is adjacent to a tile with BlobCellComponent horizontally or vertically
            var xform = Transform(target);
            var mobTile = grid.GetTileRef(xform.Coordinates);

            var mobAdjacentTiles = new[]
            {
                mobTile.GridIndices.Offset(Direction.East),
                mobTile.GridIndices.Offset(Direction.West),
                mobTile.GridIndices.Offset(Direction.North),
                mobTile.GridIndices.Offset(Direction.South)
            };
            if (mobAdjacentTiles.Any(indices => grid.GetAnchoredEntities(indices).Any(ent => HasComp<BlobTileComponent>(ent))))
            {
                if (HasComp<DestructibleComponent>(target) && !HasComp<ItemComponent>(target)&& !HasComp<SubFloorHideComponent>(target))
                {
                    if (_blobCoreSystem.TryUseAbility(uid, observerComponent.Core.Value, blobCoreComponent, blobCoreComponent.AttackCost))
                    {
                        if (_gameTiming.CurTime < blobCoreComponent.NextAction)
                            return;
                        if (blobCoreComponent.Observer != null)
                        {
                            _popup.PopupCoordinates(Loc.GetString("blob-spent-resource", ("point", blobCoreComponent.AttackCost)),
                                args.ClickLocation,
                                blobCoreComponent.Observer.Value,
                                PopupType.LargeCaution);
                        }
                        _damageableSystem.TryChangeDamage(target, blobCoreComponent.ChemDamageDict[blobCoreComponent.CurrentChem]);

                        if (blobCoreComponent.CurrentChem == BlobChemType.ExplosiveLattice)
                        {
                            _explosionSystem.QueueExplosion(target, blobCoreComponent.BlobExplosive, 4, 1, 6, maxTileBreak: 0);
                        }

                        if (blobCoreComponent.CurrentChem == BlobChemType.ElectromagneticWeb)
                        {
                            if (_random.Prob(0.2f))
                                _empSystem.EmpPulse(xform.MapPosition, 3f, 50f, 3f);
                        }

                        if (blobCoreComponent.CurrentChem == BlobChemType.BlazingOil)
                        {
                            if (TryComp<FlammableComponent>(target, out var flammable))
                            {
                                flammable.FireStacks += 2;
                                _flammable.Ignite(target, uid, flammable);
                            }
                        }
                        blobCoreComponent.NextAction =
                            _gameTiming.CurTime + TimeSpan.FromSeconds(blobCoreComponent.AttackRate);
                        _audioSystem.PlayPvs(blobCoreComponent.AttackSound, uid, AudioParams.Default);
                        return;
                    }
                }
            }
        }

        var centerTile = grid.GetLocalTilesIntersecting(
            new Box2(location.Position, location.Position), false).ToArray();

        var targetTileEmplty = false;
        foreach (var tileRef in centerTile)
        {
            if (tileRef.Tile.IsEmpty)
            {
                targetTileEmplty = true;
            }

            foreach (var ent in grid.GetAnchoredEntities(tileRef.GridIndices))
            {
                if (HasComp<BlobTileComponent>(ent))
                    return;
            }

            foreach (var entityUid in _lookup.GetEntitiesIntersecting(tileRef.GridIndices.ToEntityCoordinates(gridId.Value, _map).ToMap(EntityManager)))
            {
                if (HasComp<MobStateComponent>(entityUid) && !HasComp<BlobMobComponent>(entityUid))
                    return;
            }
        }

        var targetTile = grid.GetTileRef(location);

        var adjacentTiles = new[]
        {
            targetTile.GridIndices.Offset(Direction.East),
            targetTile.GridIndices.Offset(Direction.West),
            targetTile.GridIndices.Offset(Direction.North),
            targetTile.GridIndices.Offset(Direction.South)
        };

        if (!adjacentTiles.Any(indices =>
                grid.GetAnchoredEntities(indices).Any(ent => HasComp<BlobTileComponent>(ent))))
            return;
        var cost = blobCoreComponent.NormalBlobCost;
        if (targetTileEmplty)
        {
            cost *= 2;
        }

        if (!_blobCoreSystem.TryUseAbility(uid, observerComponent.Core.Value, blobCoreComponent, cost))
            return;

        if (targetTileEmplty)
        {
            var plating = _tileDefinitionManager["Plating"];
            var platingTile = new Tile(plating.TileId);
            grid.SetTile(location, platingTile);
        }

        _blobCoreSystem.TransformBlobTile(null,
            observerComponent.Core.Value,
            blobCoreComponent.NormalBlobTile,
            location,
            blobCoreComponent,
            transformCost: cost);
    }
    private void OnCreateFactory(EntityUid uid, BlobCoreComponent blobCoreComponent, BlobCreateFactoryActionEvent args)
    {
        if (args.Handled)
            return;

        var gridUid = args.Target.GetGridUid(EntityManager);

        if (!_map.TryGetGrid(gridUid, out var grid))
        {
            return;
        }

        var centerTile = grid.GetLocalTilesIntersecting(
            new Box2(args.Target.Position, args.Target.Position)).ToArray();

        var blobTileType = BlobTileType.None;
        EntityUid? blobTile = null;

        foreach (var tileRef in centerTile)
        {
            foreach (var ent in grid.GetAnchoredEntities(tileRef.GridIndices))
            {
                if (!TryComp<BlobTileComponent>(ent, out var blobTileComponent))
                    continue;
                blobTileType = blobTileComponent.BlobTileType;
                blobTile = ent;
                break;
            }
        }

        if (blobTileType is not BlobTileType.Normal ||
            blobTile == null)
        {
            _popup.PopupEntity(Loc.GetString("blob-target-normal-blob-invalid"), uid, uid, PopupType.Large);
            return;
        }

        var xform = Transform(blobTile.Value);

        var localPos = xform.Coordinates.Position;

        var radius = blobCoreComponent.FactoryRadiusLimit;

        var innerTiles = grid.GetLocalTilesIntersecting(
            new Box2(localPos + new Vector2(-radius, -radius), localPos + new Vector2(radius, radius)), false).ToArray();

        foreach (var tileRef in innerTiles)
        {
            foreach (var ent in grid.GetAnchoredEntities(tileRef.GridIndices))
            {
                if (!HasComp<BlobFactoryComponent>(ent))
                    continue;
                _popup.PopupEntity(Loc.GetString("Слишком близко к другой фабрике"), args.Performer, args.Performer, PopupType.Large);
                return;
            }
        }

        if (!_blobCoreSystem.CheckNearNode(args.Performer, xform.Coordinates, grid, blobCoreComponent))
            return;

        if (!_blobCoreSystem.TryUseAbility(args.Performer, uid, blobCoreComponent,
                blobCoreComponent.FactoryBlobCost))
        {
            args.Handled = true;
            return;
        }

        if (!_blobCoreSystem.TransformBlobTile(null,
                uid,
                blobCoreComponent.FactoryBlobTile,
                args.Target,
                blobCoreComponent,
                transformCost: blobCoreComponent.FactoryBlobCost))
            return;

        args.Handled = true;
    }
}
