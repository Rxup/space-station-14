using Content.Server._Lavaland.Shuttles.Components;
using Content.Server.Power.Components;
using Content.Shared._Lavaland.Shuttles;
using Content.Shared._Lavaland.Shuttles.Components;
using Content.Shared.Examine;
using Content.Shared.Power.EntitySystems;

namespace Content.Server._Lavaland.Shuttles.Systems;

/// <summary>
/// Devices placed on the mining shuttle after mapinit draw extra APC power.
/// Stock map equipment is grandfathered and keeps its normal load.
/// </summary>
public sealed partial class MiningShuttlePowerTaxSystem : EntitySystem
{
    [Dependency] private SharedPowerReceiverSystem _powerReceiver = default!;

    private EntityQuery<MiningShuttleComponent> _shuttleQuery;

    public override void Initialize()
    {
        base.Initialize();

        _shuttleQuery = GetEntityQuery<MiningShuttleComponent>();

        SubscribeLocalEvent<MiningShuttleComponent, MapInitEvent>(OnShuttleMapInit);
        SubscribeLocalEvent<ApcPowerReceiverComponent, ApcPowerReceiverMapInitEvent>(OnReceiverMapInit);
        SubscribeLocalEvent<ApcPowerReceiverComponent, EntParentChangedMessage>(OnReceiverParentChanged);
        SubscribeLocalEvent<MiningShuttlePowerTaxComponent, ExaminedEvent>(OnTaxExamined);
    }

    private void OnShuttleMapInit(Entity<MiningShuttleComponent> ent, ref MapInitEvent args)
    {
        // Walk the transform tree instead of EntityQueryEnumerator: that enumerator skips
        // paused entities, and shuttle MapInit often runs while the dummy/load map is paused.
        ExemptDescendants(ent.Owner);

        var query = AllEntityQuery<ApcPowerReceiverComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var xform))
        {
            if (xform.GridUid != ent.Owner)
                continue;

            Exempt(uid);
        }
    }

    private void OnReceiverMapInit(Entity<ApcPowerReceiverComponent> ent, ref ApcPowerReceiverMapInitEvent args)
    {
        TryApplyTax(ent);
    }

    private void OnReceiverParentChanged(Entity<ApcPowerReceiverComponent> ent, ref EntParentChangedMessage args)
    {
        var grid = Transform(ent).GridUid;
        if (grid != null && _shuttleQuery.HasComp(grid.Value))
        {
            // Transform startup fires parent-changed before the shuttle itself map-inits.
            // Those receivers are stock equipment, not player-built additions.
            if (LifeStage(grid.Value) < EntityLifeStage.MapInitialized)
            {
                Exempt(ent.Owner);
                return;
            }

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

    private void ExemptDescendants(EntityUid uid)
    {
        var enumerator = Transform(uid).ChildEnumerator;
        while (enumerator.MoveNext(out var child))
        {
            if (HasComp<ApcPowerReceiverComponent>(child))
                Exempt(child);

            ExemptDescendants(child);
        }
    }

    private void Exempt(EntityUid uid)
    {
        EnsureComp<MiningShuttlePowerExemptComponent>(uid);

        if (TryComp<ApcPowerReceiverComponent>(uid, out var receiver))
            TryRemoveTax((uid, receiver));
    }

    private void TryApplyTax(Entity<ApcPowerReceiverComponent> ent)
    {
        if (HasComp<MiningShuttlePowerTaxComponent>(ent) || HasComp<MiningShuttlePowerExemptComponent>(ent))
            return;

        var grid = Transform(ent).GridUid;
        if (grid == null || !_shuttleQuery.TryComp(grid.Value, out var shuttle))
            return;

        if (LifeStage(grid.Value) < EntityLifeStage.MapInitialized)
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
