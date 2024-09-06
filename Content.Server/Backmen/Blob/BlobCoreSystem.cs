using System.Linq;
using System.Numerics;
using System.Threading;
using Content.Server.Actions;
using Content.Server.AlertLevel;
using Content.Server.Backmen.Blob.Components;
using Content.Server.Backmen.GameTicking.Rules.Components;
using Content.Server.Backmen.Objectives;
using Content.Server.Explosion.Components;
using Content.Server.Explosion.EntitySystems;
using Content.Server.GameTicking;
using Content.Server.RoundEnd;
using Content.Server.Station.Systems;
using Content.Server.Store.Components;
using Content.Server.Store.Systems;
using Content.Shared.Alert;
using Content.Shared.Backmen.Blob;
using Content.Shared.Backmen.Blob.Components;
using Content.Shared.Damage;
using Content.Shared.Destructible;
using Content.Shared.Explosion.Components;
using Content.Shared.FixedPoint;
using Content.Shared.Objectives.Components;
using Content.Shared.Popups;
using Content.Shared.Store;
using Content.Shared.Store.Components;
using Content.Shared.Weapons.Melee;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server.Backmen.Blob;

public sealed class BlobCoreSystem : SharedBlobCoreSystem
{
    [Dependency] private readonly AlertsSystem _alerts = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly GameTicker _gameTicker = default!;
    [Dependency] private readonly ExplosionSystem _explosionSystem = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly StationSystem _stationSystem = default!;
    [Dependency] private readonly AlertLevelSystem _alertLevelSystem = default!;
    [Dependency] private readonly RoundEndSystem _roundEndSystem = default!;
    [Dependency] private readonly MetaDataSystem _metaDataSystem = default!;
    [Dependency] private readonly ActionsSystem _action = default!;
    [Dependency] private readonly MapSystem _map = default!;
    [Dependency] private readonly StoreSystem _storeSystem = default!;

    private EntityQuery<BlobTileComponent> _tile;
    private EntityQuery<BlobFactoryComponent> _factory;

    [ValidatePrototypeId<EntityPrototype>] private const string BlobCaptureObjective = "BlobCaptureObjective";
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
    }

    private void OnTerminating(EntityUid uid, BlobCoreComponent component, ref EntityTerminatingEvent args)
    {
        if (component.Observer != null && !TerminatingOrDeleted(component.Observer.Value) && !EntityManager.IsQueuedForDeletion(component.Observer.Value))
        {
            QueueDel(component.Observer.Value);
        }
    }

    #region Objective

    private void OnBlobCaptureInfoAdd(Entity<BlobCaptureConditionComponent> ent, ref ObjectiveAssignedEvent args)
    {
        if (args.Mind?.OwnedEntity == null)
        {
            args.Cancelled = true;
            return;
        }
        if (!TryComp<BlobObserverComponent>(args.Mind.OwnedEntity, out var blobObserverComponent)
            || !TryComp<BlobCoreComponent>(blobObserverComponent.Core, out var blobCoreComponent))
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

        if (args.Mind?.OwnedEntity == null)
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

        args.Progress = (float) blobCoreComponent.BlobTiles.Count / (float) component.Target;
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
        var xform = Transform(blobCoreUid);

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

    [ValidatePrototypeId<EntityPrototype>] private const string ActionHelpBlob = "ActionHelpBlob";
    [ValidatePrototypeId<EntityPrototype>] private const string ActionSwapBlobChem = "ActionSwapBlobChem";
    [ValidatePrototypeId<EntityPrototype>] private const string ActionTeleportBlobToCore = "ActionTeleportBlobToCore";
    [ValidatePrototypeId<EntityPrototype>] private const string ActionTeleportBlobToNode = "ActionTeleportBlobToNode";
    [ValidatePrototypeId<EntityPrototype>] private const string ActionCreateBlobFactory = "ActionCreateBlobFactory";
    [ValidatePrototypeId<EntityPrototype>] private const string ActionCreateBlobResource = "ActionCreateBlobResource";
    [ValidatePrototypeId<EntityPrototype>] private const string ActionCreateBlobNode = "ActionCreateBlobNode";
    [ValidatePrototypeId<EntityPrototype>] private const string ActionCreateBlobbernaut = "ActionCreateBlobbernaut";
    [ValidatePrototypeId<EntityPrototype>] private const string ActionSplitBlobCore = "ActionSplitBlobCore";
    [ValidatePrototypeId<EntityPrototype>] private const string ActionSwapBlobCore = "ActionSwapBlobCore";

    private void OnStartup(EntityUid uid, BlobCoreComponent component, ComponentStartup args)
    {
        var store = EnsureComp<StoreComponent>(uid);
        store.CurrencyWhitelist.Add(BlobMoney);

        ChangeBlobPoint(uid, 0, component);

        if (_tile.TryGetComponent(uid, out var blobTileComponent))
        {
            blobTileComponent.Core = uid;
            blobTileComponent.Color = component.ChemСolors[component.CurrentChem];
            Dirty(uid, blobTileComponent);
        }

        component.BlobTiles.Add(uid);

        ChangeChem(uid, component.DefaultChem, component);

        _action.AddAction(uid, ref component.ActionHelpBlob, ActionHelpBlob);
        _action.AddAction(uid, ref component.ActionSwapBlobChem, ActionSwapBlobChem);
        _action.AddAction(uid, ref component.ActionTeleportBlobToCore, ActionTeleportBlobToCore);
        _action.AddAction(uid, ref component.ActionTeleportBlobToNode, ActionTeleportBlobToNode);
        _action.AddAction(uid, ref component.ActionCreateBlobFactory, ActionCreateBlobFactory);
        _action.AddAction(uid, ref component.ActionCreateBlobResource, ActionCreateBlobResource);
        _action.AddAction(uid, ref component.ActionCreateBlobNode, ActionCreateBlobNode);
        _action.AddAction(uid, ref component.ActionCreateBlobbernaut, ActionCreateBlobbernaut);
        _action.AddAction(uid, ref component.ActionSplitBlobCore, ActionSplitBlobCore);
        _action.AddAction(uid, ref component.ActionSwapBlobCore, ActionSwapBlobCore);
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
/*
                foreach (var compBlobPod in blobFactoryComponent.BlobPods)
                {
                    if (TryComp<SmokeOnTriggerComponent>(compBlobPod, out var smokeOnTriggerComponent))
                    {
                        smokeOnTriggerComponent.SmokeColor = component.ChemСolors[newChem];
                    }
                }
                */
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
        while (blobCoreQuery.MoveNext(out var ent, out var comp))
        {
            if (TerminatingOrDeleted(ent))
            {
                continue;
            }
            isAllDie++;
        }

        if (isAllDie <= 1)
        {
            var blobFactoryQuery = EntityQueryEnumerator<BlobRuleComponent>();
            while (blobFactoryQuery.MoveNext(out _, out var blobRuleComp))
            {
                if (blobRuleComp.Stage == BlobStage.Critical ||
                    blobRuleComp.Stage == BlobStage.Begin)
                {
                    _alertLevelSystem.SetLevel(stationUid!.Value, "green", true, true, true, false);
                    _roundEndSystem.CancelRoundEndCountdown(null, false);
                }
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

    public bool TransformBlobTile(EntityUid? oldTileUid, EntityUid coreTileUid, string newBlobTileProto,
        EntityCoordinates coordinates, BlobCoreComponent? blobCore = null, bool returnCost = true,
        FixedPoint2? transformCost = null)
    {
        if (!Resolve(coreTileUid, ref blobCore))
            return false;
        if (oldTileUid != null)
        {
            QueueDel(oldTileUid.Value);
            blobCore.BlobTiles.Remove(oldTileUid.Value);
        }
        var tileBlob = EntityManager.SpawnEntity(newBlobTileProto, coordinates);

        if (_tile.TryGetComponent(tileBlob, out var blobTileComponent))
        {
            blobTileComponent.ReturnCost = returnCost;
            blobTileComponent.Core = coreTileUid;
            blobTileComponent.Color = blobCore.ChemСolors[blobCore.CurrentChem];
            Dirty(tileBlob, blobTileComponent);

            var explosionResistance = EnsureComp<ExplosionResistanceComponent>(tileBlob);

            if (blobCore.CurrentChem == BlobChemType.ExplosiveLattice)
            {
                _explosionSystem.SetExplosionResistance(tileBlob, 0f, explosionResistance);
            }
        }
        if (blobCore.Observer != null && transformCost != null)
        {
            _popup.PopupEntity(Loc.GetString("blob-spent-resource", ("point", transformCost)),
                tileBlob,
                blobCore.Observer.Value,
                PopupType.LargeCaution);
        }
        blobCore.BlobTiles.Add(tileBlob);
        return true;
    }

    public bool RemoveBlobTile(EntityUid tileUid, EntityUid coreTileUid, BlobCoreComponent? blobCore = null)
    {
        if (!Resolve(coreTileUid, ref blobCore))
            return false;

        QueueDel(tileUid);
        blobCore.BlobTiles.Remove(tileUid);

        return true;
    }

    [ValidatePrototypeId<AlertPrototype>]
    private const string BlobResource = "BlobResource";

    [ValidatePrototypeId<CurrencyPrototype>]
    private const string BlobMoney = "BlobPoint";

    private readonly ReaderWriterLockSlim _pointsChange = new();

    public bool ChangeBlobPoint(EntityUid uid, FixedPoint2 amount, BlobCoreComponent? component = null, StoreComponent? store = null)
    {
        if (!Resolve(uid, ref component) || !Resolve(uid, ref store))
            return false;

        if (_pointsChange.TryEnterWriteLock(1000))
        {
            if (_storeSystem.TryAddCurrency(new Dictionary<string, FixedPoint2>
                    {
                        { BlobMoney, amount }
                    },
                    uid,
                    store))
            {
                var pt = store.Balance.GetValueOrDefault(BlobMoney);

                if (component.Observer != null)
                    _alerts.ShowAlert(component.Observer.Value, BlobResource, (short) Math.Clamp(Math.Round(pt.Float() / 10f), 0, 16));

                _pointsChange.ExitWriteLock();
                return true;
            }
            _pointsChange.ExitWriteLock();
            return false;
        }

        return false;
    }

    public bool TryUseAbility(EntityUid uid, EntityUid coreUid, BlobCoreComponent component, FixedPoint2 abilityCost, StoreComponent? store = null)
    {
        if (!Resolve(coreUid, ref store))
            return false;

        var pt = store.Balance.GetValueOrDefault(BlobMoney);
        if (pt < abilityCost)
        {
            _popup.PopupEntity(Loc.GetString("blob-not-enough-resources"), uid, uid, PopupType.Large);
            return false;
        }

        return ChangeBlobPoint(coreUid, -abilityCost, component, store);
    }

    public bool CheckNearNode(EntityUid observer, EntityCoordinates coords, Entity<MapGridComponent> grid, BlobCoreComponent core)
    {
        var radius = 3f;

        var innerTiles = _map.GetLocalTilesIntersecting(grid,grid,
            new Box2(coords.Position + new Vector2(-radius, -radius), coords.Position + new Vector2(radius, radius)), false).ToArray();

        var queryNode = GetEntityQuery<BlobNodeComponent>();
        var queryCore = GetEntityQuery<BlobCoreComponent>();
        foreach (var tileRef in innerTiles)
        {
            foreach (var ent in _map.GetAnchoredEntities(grid,grid,tileRef.GridIndices))
            {
                if (queryNode.HasComponent(ent) || queryCore.HasComponent(ent))
                    return true;
            }
        }

        _popup.PopupCoordinates(Loc.GetString("blob-target-nearby-not-node"), coords, observer, PopupType.Large);
        return false;
    }
}
