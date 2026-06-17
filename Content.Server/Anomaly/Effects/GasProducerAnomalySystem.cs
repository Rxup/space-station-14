using Content.Server.Atmos.EntitySystems;
using Content.Server.Anomaly.Components;
using Content.Shared.Anomaly.Components;
using Content.Shared.Atmos;
using Robust.Shared.Random;
using System.Linq;
using System.Numerics;
using Robust.Shared.Map.Components;

namespace Content.Server.Anomaly.Effects;

/// <summary>
/// This handles <see cref="GasProducerAnomalyComponent"/> and the events from <seealso cref="AnomalySystem"/>
/// </summary>
public sealed partial class GasProducerAnomalySystem : EntitySystem
{
    [Dependency] private AtmosphereSystem _atmosphere = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedMapSystem _map = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<GasProducerAnomalyComponent, AnomalySupercriticalEvent>(OnSupercritical);
    }

    private void OnSupercritical(EntityUid uid, GasProducerAnomalyComponent component, ref AnomalySupercriticalEvent args)
    {
        if (!component.ReleaseOnMaxSeverity)
            return;

        ReleaseGas(uid, component.ReleasedGas, component.SuperCriticalMoleAmount, component.spawnRadius, component.tileCount, component.tempChange);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<GasProducerAnomalyComponent, AnomalyComponent>();
        while (query.MoveNext(out var ent, out var comp, out var anom))
        {
            if (!comp.ReleasePassively)
                continue;

            var moles = comp.PassiveMoleAmount * anom.Severity * frameTime;
            if (moles <= 0)
                continue;

            // Passive release only affects the host tile; tempChange is reserved for supercritical bursts.
            ReleaseGas(ent, comp.ReleasedGas, moles, comp.spawnRadius, 0, 0);
        }
    }

    private void ReleaseGas(EntityUid uid, Gas gas, float mols, float radius, int count, float temp)
    {
        var xform = Transform(uid);

        if (!TryComp<MapGridComponent>(xform.GridUid, out var grid))
            return;

        var localpos = xform.Coordinates.Position;
        var tilerefs = _map.GetLocalTilesIntersecting(
            xform.GridUid.Value,
            grid,
            new Box2(localpos + new Vector2(-radius, -radius), localpos + new Vector2(radius, radius)))
            .ToArray();

        if (tilerefs.Length == 0)
            return;

        var mixture = _atmosphere.GetTileMixture((uid, xform), true);
        if (mixture != null)
        {
            mixture.AdjustMoles(gas, mols);
            mixture.Temperature += temp;
        }

        if (count == 0)
            return;

        _random.Shuffle(tilerefs);
        var amountCounter = 0;
        foreach (var tileref in tilerefs)
        {
            var mix = _atmosphere.GetTileMixture(xform.GridUid, xform.MapUid, tileref.GridIndices, true);
            amountCounter++;
            if (mix is not { })
                continue;

            mix.AdjustMoles(gas, mols);
            mix.Temperature += temp;

            if (amountCounter >= count)
                return;
        }
    }
}

