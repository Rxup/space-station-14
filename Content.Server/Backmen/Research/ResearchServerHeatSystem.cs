using Content.Server.Atmos.EntitySystems;
using Content.Server.Power.Components;
using Content.Shared.Atmos;
using Robust.Server.GameObjects;
using Robust.Shared.Timing;

namespace Content.Server.Backmen.Research;

public sealed partial class ResearchServerHeatSystem : EntitySystem
{
    [Dependency] private AtmosphereSystem _atmosphere = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private TransformSystem _transform = default!;

    private readonly List<GasMixture> _environments = new();

    public override void Update(float frameTime)
    {
        var query = EntityQueryEnumerator<ResearchServerHeatProducingComponent, ApcPowerReceiverComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var heat, out var power, out var xform))
        {
            if (!power.Powered || power.PowerReceived <= 0f)
                continue;

            if (_timing.CurTime < heat.NextSecond)
                continue;

            heat.NextSecond = _timing.CurTime + TimeSpan.FromSeconds(1);

            var energy = power.PowerReceived * heat.HeatScale;
            if (energy <= 0f)
                continue;

            var position = _transform.GetGridTilePositionOrDefault((uid, xform));
            _environments.Clear();

            if (_atmosphere.GetTileMixture(xform.GridUid, xform.MapUid, position, true) is { } tileMix)
                _environments.Add(tileMix);

            if (xform.GridUid != null)
            {
                var enumerator = _atmosphere.GetAdjacentTileMixtures(xform.GridUid.Value, position, false, true);
                while (enumerator.MoveNext(out var mix))
                {
                    _environments.Add(mix);
                }
            }

            if (_environments.Count == 0)
                continue;

            var heatPerTile = energy / _environments.Count;
            foreach (var env in _environments)
            {
                _atmosphere.AddHeat(env, heatPerTile);
            }
        }
    }
}
