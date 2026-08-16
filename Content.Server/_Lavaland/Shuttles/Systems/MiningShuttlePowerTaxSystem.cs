using Content.Server._Lavaland.Shuttles.Components;
using Content.Server.Power.Components;
using Content.Server.Power.EntitySystems;
using Content.Shared._Lavaland.Shuttles.Components;
using Content.Shared.Examine;
using Content.Shared.Power.EntitySystems;

namespace Content.Server._Lavaland.Shuttles.Systems;

/// <summary>
/// Devices placed on the mining shuttle after mapinit draw extra APC power.
/// </summary>
public sealed partial class MiningShuttlePowerTaxSystem : EntitySystem
{
    [Dependency] private SharedPowerReceiverSystem _powerReceiver = default!;

    private EntityQuery<MiningShuttleComponent> _shuttleQuery;
    private EntityQuery<MiningShuttlePowerTrackerComponent> _trackerQuery;

    public override void Initialize()
    {
        base.Initialize();

        _shuttleQuery = GetEntityQuery<MiningShuttleComponent>();
        _trackerQuery = GetEntityQuery<MiningShuttlePowerTrackerComponent>();

        SubscribeLocalEvent<MiningShuttleComponent, MapInitEvent>(OnShuttleMapInit);
        SubscribeLocalEvent<ApcPowerReceiverComponent, MapInitEvent>(OnReceiverMapInit, after: [typeof(PowerChargeSystem)]);
        SubscribeLocalEvent<ApcPowerReceiverComponent, EntParentChangedMessage>(OnReceiverParentChanged);
        SubscribeLocalEvent<MiningShuttlePowerTaxComponent, ExaminedEvent>(OnTaxExamined);
    }

    private void OnShuttleMapInit(Entity<MiningShuttleComponent> ent, ref MapInitEvent args)
    {
        var tracker = EnsureComp<MiningShuttlePowerTrackerComponent>(ent);

        var query = EntityQueryEnumerator<ApcPowerReceiverComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var xform))
        {
            if (xform.GridUid != ent.Owner)
                continue;

            tracker.MapInitReceivers.Add(uid);
        }
    }

    private void OnReceiverMapInit(Entity<ApcPowerReceiverComponent> ent, ref MapInitEvent args)
    {
        TryApplyTax(ent);
    }

    private void OnReceiverParentChanged(Entity<ApcPowerReceiverComponent> ent, ref EntParentChangedMessage args)
    {
        var grid = Transform(ent).GridUid;
        if (grid != null && _shuttleQuery.HasComp(grid.Value))
        {
            TryApplyTax(ent);
            return;
        }

        TryRemoveTax(ent);
    }

    private void OnTaxExamined(Entity<MiningShuttlePowerTaxComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        args.PushMarkup(Loc.GetString("mining-shuttle-power-tax-examine", ("multiplier", ent.Comp.Multiplier)));
    }

    private void TryApplyTax(Entity<ApcPowerReceiverComponent> ent)
    {
        if (HasComp<MiningShuttlePowerTaxComponent>(ent))
            return;

        var grid = Transform(ent).GridUid;
        if (grid == null || !_shuttleQuery.TryComp(grid.Value, out var shuttle))
            return;

        if (!_trackerQuery.TryComp(grid.Value, out var tracker) || tracker.MapInitReceivers.Contains(ent.Owner))
            return;

        var tax = EnsureComp<MiningShuttlePowerTaxComponent>(ent);
        tax.Multiplier = shuttle.ExtraPowerMultiplier;
        Dirty(ent.Owner, tax);

        _powerReceiver.SetLoad(ent.Owner, ent.Comp.Load);
    }

    private void TryRemoveTax(Entity<ApcPowerReceiverComponent> ent)
    {
        if (!TryComp<MiningShuttlePowerTaxComponent>(ent, out var tax))
            return;

        var baseLoad = ent.Comp.Load / tax.Multiplier;
        RemComp<MiningShuttlePowerTaxComponent>(ent);
        _powerReceiver.SetLoad(ent.Owner, baseLoad);
    }
}
