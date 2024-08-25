using System.Linq;
using System.Numerics;
using Content.Server.Actions;
using Content.Server.AlertLevel;
using Content.Server.Backmen.Blob.Components;
using Content.Server.Backmen.GameTicking.Rules.Components;
using Content.Server.Backmen.Objectives;
using Content.Server.Explosion.EntitySystems;
using Content.Server.GameTicking;
using Content.Server.RoundEnd;
using Content.Server.Station.Systems;
using Content.Shared.Alert;
using Content.Shared.Backmen.Blob;
using Content.Shared.Backmen.Blob.Components;
using Content.Shared.Damage;
using Content.Shared.Destructible;
using Content.Shared.Explosion.Components;
using Content.Shared.FixedPoint;
using Content.Shared.Objectives.Components;
using Content.Shared.Popups;
using Content.Shared.Weapons.Melee;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Network;
using Robust.Shared.Player;

namespace Content.Server.Backmen.Blob;

public sealed class BlobCoreSystem : SharedBlobCoreSystem
{
    [Dependency] private readonly AlertsSystem _alerts = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly GameTicker _gameTicker = default!;
    [Dependency] private readonly ExplosionSystem _explosionSystem = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly StationSystem _stationSystem = default!;
    [Dependency] private readonly AlertLevelSystem _alertLevelSystem = default!;
    [Dependency] private readonly RoundEndSystem _roundEndSystem = default!;
    [Dependency] private readonly MetaDataSystem _metaDataSystem = default!;
    [Dependency] private readonly ActionsSystem _action = default!;
    [Dependency] private readonly MapSystem _map = default!;

    private EntityQuery<BlobTileComponent> _tile;
    private EntityQuery<BlobFactoryComponent> _factory;
    private EntityQuery<BlobNodeComponent> _node;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BlobCoreComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<BlobCoreComponent, DestructionEventArgs>(OnDestruction);

        SubscribeLocalEvent<BlobCoreComponent, PlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<BlobCoreComponent, EntityTerminatingEvent>(OnTerminating);

        SubscribeLocalEvent<BlobCaptureConditionComponent, ObjectiveGetProgressEvent>(OnBlobCaptureProgress);
        SubscribeLocalEvent<BlobCaptureConditionComponent, ObjectiveAfterAssignEvent>(OnBlobCaptureInfo);
        SubscribeLocalEvent<BlobCaptureConditionComponent, ObjectiveAssignedEvent>(OnBlobCaptureInfoAdd);


        _tile = GetEntityQuery<BlobTileComponent>();
        _factory = GetEntityQuery<BlobFactoryComponent>();
        _node = GetEntityQuery<BlobNodeComponent>();
    }

    private void OnTerminating(EntityUid uid, BlobCoreComponent component, ref EntityTerminatingEvent args)
    {
        OnDestruction(uid, component, new DestructionEventArgs());
    }

    #region Objective

    private void OnBlobCaptureInfoAdd(Entity<BlobCaptureConditionComponent> ent, ref ObjectiveAssignedEvent args)
    {
        if (args.Mind.OwnedEntity == null)
        {
            args.Cancelled = true;
            return;
        }
        if (!TryComp<BlobObserverComponent>(args.Mind.OwnedEntity, out var blobObserverComponent)
            || !HasComp<BlobCoreComponent>(blobObserverComponent.Core))
        {
            args.Cancelled = true;
            return;
        }

        var station = _stationSystem.GetOwningStation(blobObserverComponent.Core);
        if (station == null)
        {
            args.Cancelled = true;
            return;
        }

        ent.Comp.Target = CompOrNull<StationBlobConfigComponent>(station)?.StageTheEnd ?? StationBlobConfigComponent.DefaultStageEnd;
    }

    private void OnBlobCaptureInfo(EntityUid uid, BlobCaptureConditionComponent component, ref ObjectiveAfterAssignEvent args)
    {
        _metaDataSystem.SetEntityName(uid,Loc.GetString("objective-condition-blob-capture-title"));
        _metaDataSystem.SetEntityDescription(uid,Loc.GetString("objective-condition-blob-capture-description", ("count", component.Target)));
    }

    private void OnBlobCaptureProgress(EntityUid uid, BlobCaptureConditionComponent component, ref ObjectiveGetProgressEvent args)
    {
        // prevent divide-by-zero
        if (component.Target == 0)
        {
            args.Progress = 1;
            return;
        }

        if (args.Mind.OwnedEntity == null || TerminatingOrDeleted(args.Mind.OwnedEntity))
        {
            args.Progress = 0;
            return;
        }

        if (!TryComp<BlobObserverComponent>(args.Mind.OwnedEntity, out var blobObserverComponent)
            || !TryComp<BlobCoreComponent>(blobObserverComponent.Core, out var blobCoreComponent))
        {
            args.Progress = 0;
            return;
        }

        args.Progress = blobCoreComponent.BlobTiles.Count / component.Target;
    }
    #endregion

    private void OnPlayerAttached(EntityUid uid, BlobCoreComponent component, PlayerAttachedEvent args)
    {
        var xform = Transform(uid);
        if (!HasComp<MapGridComponent>(xform.GridUid))
            return;

        CreateBlobObserver(uid, args.Player.UserId, component);
    }

    public bool CreateBlobObserver(EntityUid blobCoreUid, NetUserId userId, BlobCoreComponent? core = null)
    {
        if (!Resolve(blobCoreUid, ref core))
            return false;

        var blobRule = EntityQuery<BlobRuleComponent>().FirstOrDefault();

        if (blobRule == null)
        {
            _gameTicker.StartGameRule("Blob", out _);
        }
        var ev = new CreateBlobObserverEvent(userId);
        RaiseLocalEvent(blobCoreUid, ev, true);

        return !ev.Cancelled;
    }

    private void OnStartup(EntityUid uid, BlobCoreComponent component, ComponentStartup args)
    {
        ChangeBlobPoint((uid, component), 0);

        if (_tile.TryGetComponent(uid, out var blobTileComponent))
        {
            blobTileComponent.Core = (uid, component);
            blobTileComponent.Color = component.ChemСolors[component.CurrentChem];
            Dirty(uid, blobTileComponent);
        }

        component.BlobTiles.Add(uid);

        ChangeChem(uid, component.DefaultChem, component);

        foreach (var action in component.Actions)
        {
            var actionUid = component.Actions[action.Key];
            _action.AddAction(uid, ref actionUid, action.Key);
            component.Actions[action.Key] = actionUid;
        }
    }

    public void ChangeChem(EntityUid uid, BlobChemType newChem, BlobCoreComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        if (newChem == component.CurrentChem)
            return;

        var oldChem = component.CurrentChem;
        component.CurrentChem = newChem;
        foreach (var blobTile in component.BlobTiles)
        {
            if (!_tile.TryGetComponent(blobTile, out var blobTileComponent))
                continue;

            blobTileComponent.Color = component.ChemСolors[newChem];
            Dirty(blobTile, blobTileComponent);

            if (_factory.TryGetComponent(blobTile, out var blobFactoryComponent))
            {
                if (TryComp<BlobbernautComponent>(blobFactoryComponent.Blobbernaut, out var blobbernautComponent))
                {
                    blobbernautComponent.Color = component.ChemСolors[newChem];
                    Dirty(blobFactoryComponent.Blobbernaut.Value, blobbernautComponent);

                    if (TryComp<MeleeWeaponComponent>(blobFactoryComponent.Blobbernaut, out var meleeWeaponComponent))
                    {
                        var blobbernautDamage = new DamageSpecifier();
                        foreach (var keyValuePair in component.ChemDamageDict[component.CurrentChem].DamageDict)
                        {
                            blobbernautDamage.DamageDict.Add(keyValuePair.Key, keyValuePair.Value * 0.8f);
                        }
                        meleeWeaponComponent.Damage = blobbernautDamage;
                    }

                    ChangeBlobEntChem(blobFactoryComponent.Blobbernaut.Value, oldChem, newChem);
                }
            }

            ChangeBlobEntChem(blobTile, oldChem, newChem);
        }
    }

    private void OnDestruction(EntityUid uid, BlobCoreComponent component, DestructionEventArgs args)
    {
        if (component.Observer != null)
        {
            QueueDel(component.Observer.Value);
        }

        foreach (var blobTile in component.BlobTiles)
        {
            if (!_tile.TryGetComponent(blobTile, out var blobTileComponent))
                continue;

            blobTileComponent.Core = null;
            blobTileComponent.Color = Color.White;
            Dirty(blobTile, blobTileComponent);
        }

        var stationUid = _stationSystem.GetOwningStation(uid);
        var blobCoreQuery = EntityQueryEnumerator<BlobCoreComponent>();
        var isAllDie = 0;
        while (blobCoreQuery.MoveNext(out var ent, out _))
        {
            if (TerminatingOrDeleted(ent))
            {
                continue;
            }
            isAllDie++;
        }

        if (isAllDie <= 1)
        {
            var blobRuleQuery = EntityQueryEnumerator<BlobRuleComponent>();
            while (blobRuleQuery.MoveNext(out _, out var blobRuleComp))
            {
                if (blobRuleComp.Stage == BlobStage.TheEnd ||
                    blobRuleComp.Stage == BlobStage.Default ||
                    stationUid == null)
                    continue;

                _alertLevelSystem.SetLevel(stationUid.Value, "green", true, true, true);
                _roundEndSystem.CancelRoundEndCountdown(null, false);
                blobRuleComp.Stage = BlobStage.Default;
            }
        }
        QueueDel(uid);
    }

    private void ChangeBlobEntChem(EntityUid uid, BlobChemType oldChem, BlobChemType newChem)
    {
        var explosionResistance = EnsureComp<ExplosionResistanceComponent>(uid);
        if (oldChem == BlobChemType.ExplosiveLattice)
        {
            _explosionSystem.SetExplosionResistance(uid, 0.3f, explosionResistance);
        }
        switch (newChem)
        {
            case BlobChemType.ExplosiveLattice:
                _damageable.SetDamageModifierSetId(uid, "ExplosiveLatticeBlob");
                _explosionSystem.SetExplosionResistance(uid, 0f, explosionResistance);
                break;
            case BlobChemType.ElectromagneticWeb:
                _damageable.SetDamageModifierSetId(uid, "ElectromagneticWebBlob");
                break;
            case BlobChemType.ReactiveSpines:
                _damageable.SetDamageModifierSetId(uid, "ReactiveSpinesBlob");
                break;
            default:
                _damageable.SetDamageModifierSetId(uid, "BaseBlob");
                break;
        }
    }

    public bool TransformBlobTile(
        Entity<BlobTileComponent>? oldTileUid,
        Entity<BlobCoreComponent> blobCore,
        Entity<BlobNodeComponent>? nearNode,
        BlobTileType newBlobTile,
        EntityCoordinates coordinates)
    {
        if (oldTileUid != null)
        {
            if (oldTileUid.Value.Comp.Core != blobCore)
                return false;

            RemoveBlobTile(oldTileUid.Value, blobCore);
        }

        var blobTileUid = EntityManager.SpawnEntity(blobCore.Comp.TilePrototypes[newBlobTile], coordinates);

        if (!_tile.TryGetComponent(blobTileUid, out var blobTileComp))
        {
            // Blob somehow spawned not a blob tile?
            return false;
        }

        AddBlobTile((blobTileUid, blobTileComp), newBlobTile, blobCore);

        // God please forgive me for this
        if (nearNode != null && nearNode.Value.Comp.ConnectedTiles.ContainsKey(blobTileComp.BlobTileType))
            nearNode.Value.Comp.ConnectedTiles[blobTileComp.BlobTileType] = blobTileUid;

        Dirty(blobTileUid, blobTileComp);

        var explosionResistance = EnsureComp<ExplosionResistanceComponent>(blobTileUid);

        if (blobCore.Comp.CurrentChem == BlobChemType.ExplosiveLattice)
        {
            _explosionSystem.SetExplosionResistance(blobTileUid, 0f, explosionResistance);
        }

        return true;
    }

    public void AddBlobTile(Entity<BlobTileComponent> tile, BlobTileType type, Entity<BlobCoreComponent> core)
    {
        var coreComp = core.Comp;
        var tileComp = tile.Comp;

        coreComp.BlobTiles.Add(tile);
        tileComp.Color = coreComp.ChemСolors[coreComp.CurrentChem];
        tileComp.Core = core;
        tileComp.BlobTileType = type;
    }

    public void RemoveBlobTile(EntityUid tile, Entity<BlobCoreComponent> core)
    {
        QueueDel(tile);
        core.Comp.BlobTiles.Remove(tile);
    }

    public void RemoveTileWithReturnCost(Entity<BlobTileComponent> target, Entity<BlobCoreComponent> core)
    {
        RemoveBlobTile(target, core);

        FixedPoint2 returnCost = 0;
        var tileComp = target.Comp;

        if (target.Comp.ReturnCost)
        {
            returnCost = core.Comp.BlobTileCosts[tileComp.BlobTileType];
        }

        if (returnCost <= 0)
            return;

        if (core.Comp.Observer != null)
        {
            _popup.PopupCoordinates(Loc.GetString("blob-get-resource", ("point", returnCost)),
                Transform(target).Coordinates,
                core.Comp.Observer.Value,
                PopupType.LargeGreen);
        }

        ChangeBlobPoint(core, returnCost);
    }

    [ValidatePrototypeId<AlertPrototype>]
    private const string BlobResource = "BlobResource";

    public void ChangeBlobPoint(Entity<BlobCoreComponent> core, FixedPoint2 amount)
    {
        core.Comp.Points += amount;

        if (core.Comp.Observer != null)
            _alerts.ShowAlert(core.Comp.Observer.Value, BlobResource, (short) Math.Clamp(Math.Round(core.Comp.Points.Float() / 10f), 0, 16));
    }

    public bool TryUseAbility(Entity<BlobCoreComponent> core, FixedPoint2 abilityCost, bool popupCursor = false)
    {
        var comp = core.Comp;
        var points = comp.Points;
        var observer = core.Comp.Observer;

        if (observer == null)
            return false;

        if (points < abilityCost)
        {
            _popup.PopupEntity(Loc.GetString("blob-not-enough-resources", ("point", abilityCost - points)), observer.Value, PopupType.Large);
            return false;
        }

        if (popupCursor)
        {
            _popup.PopupCursor(
                Loc.GetString("blob-spent-resource", ("point", (short) Math.Clamp(Math.Round(abilityCost.Float() / 10f), 0, 16))),
                observer.Value,
                PopupType.LargeCaution);
        }
        else
        {
            _popup.PopupEntity(
                Loc.GetString("blob-spent-resource", ("point", abilityCost.Value)),
                observer.Value,
                PopupType.LargeCaution);
        }

        ChangeBlobPoint(core, -abilityCost);
        return true;
    }

    /// <summary>
    /// Gets the nearest Blob node from some EntityCoordinates.
    /// </summary>
    /// <param name="coords">The EntityCoordinates to check from.</param>
    /// <param name="radius">Radius to check from coords.</param>
    /// <returns>Nearest blob node with it's component, null if wasn't founded.</returns>
    public Entity<BlobNodeComponent>? GetNearNode(
        EntityCoordinates coords,
        float radius = 3f)
    {
        var gridUid = _transform.GetGrid(coords)!.Value;

        if (!TryComp<MapGridComponent>(gridUid, out var grid))
            return null;

        var nearestDistance = float.MaxValue;
        var nodeComponent = new BlobNodeComponent();
        var nearestEntityUid = EntityUid.Invalid;

        var innerTiles = _map.GetLocalTilesIntersecting(
                gridUid,
                grid,
                new Box2(coords.Position + new Vector2(-radius, -radius),
                    coords.Position + new Vector2(radius, radius)),
                false)
            .ToArray();

        foreach (var tileRef in innerTiles)
        {
            foreach (var ent in _map.GetAnchoredEntities(gridUid, grid, tileRef.GridIndices))
            {
                if (!_node.TryComp(ent, out var nodeComp))
                    continue;
                var tileCords = Transform(ent).Coordinates;
                var distance = Vector2.Distance(coords.Position, tileCords.Position);

                if (!(distance < nearestDistance))
                    continue;

                nearestDistance = distance;
                nearestEntityUid = ent;
                nodeComponent = nodeComp;
            }
        }

        return nearestDistance > radius ? null : (nearestEntityUid, nodeComponent);
    }
}
