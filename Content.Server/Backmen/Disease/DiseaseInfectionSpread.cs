using System.Threading;
using System.Threading.Tasks;
using Content.Server.Backmen.Disease.Components;
using Content.Server.Backmen.Disease.Effects;
using Robust.Shared.CPUJob.JobQueues;
using Robust.Shared.Timing;

namespace Content.Server.Backmen.Disease;

public sealed class DiseaseInfectionSpread : Job<object>
{
    private readonly DiseaseInfectionSpreadEvent _ev;
    private readonly DiseaseEffectSystem _effectSystem;

    public DiseaseInfectionSpread(DiseaseInfectionSpreadEvent ev, DiseaseEffectSystem effectSystem, double maxTime, CancellationToken cancellation = default) : base(maxTime, cancellation)
    {
        _ev = ev;
        _effectSystem = effectSystem;
    }

    public DiseaseInfectionSpread(DiseaseInfectionSpreadEvent ev, DiseaseEffectSystem effectSystem, double maxTime, IStopwatch stopwatch, CancellationToken cancellation = default) : base(maxTime, stopwatch, cancellation)
    {
        _ev = ev;
        _effectSystem = effectSystem;
    }

    protected override async Task<object?> Process()
    {
        _effectSystem.DoSpread(_ev.Owner, _ev.Disease, _ev.Range);
        return null;
    }
}
