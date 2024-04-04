using System.Threading;
using System.Threading.Tasks;
using Content.Server.Backmen.Species.Shadowkin.Components;
using Content.Server.Backmen.Species.Shadowkin.Systems;
using Content.Shared.Backmen.Species.Shadowkin.Components;
using Robust.Server.GameObjects;
using Robust.Shared.CPUJob.JobQueues;
using Robust.Shared.Timing;

namespace Content.Server.Backmen.Species.Shadowkin.Jobs;

public sealed class ShadowkinLightJob : Job<object>
{
    private readonly ShadowkinDarkenSystem _system;
    private readonly TransformSystem _transform;
    private readonly EntityLookupSystem _lookup;
    private readonly Entity<ShadowkinDarkSwappedComponent, TransformComponent> _ent;

    public ShadowkinLightJob(ShadowkinDarkenSystem system, TransformSystem transform, EntityLookupSystem lookup, Entity<ShadowkinDarkSwappedComponent, TransformComponent> ent, double maxTime, CancellationToken cancellation = default) : base(maxTime, cancellation)
    {
        _system = system;
        _transform = transform;
        _lookup = lookup;
        _ent = ent;
    }

    public ShadowkinLightJob(ShadowkinDarkenSystem system, TransformSystem transform, EntityLookupSystem lookup, Entity<ShadowkinDarkSwappedComponent, TransformComponent> ent, double maxTime, IStopwatch stopwatch, CancellationToken cancellation = default) : base(maxTime, stopwatch, cancellation)
    {
        _system = system;
        _transform = transform;
        _lookup = lookup;
        _ent = ent;
    }

    private readonly HashSet<Entity<ShadowkinLightComponent>> _lightQuery = new();

    protected override async Task<object?> Process()
    {
        var transform = _transform.GetMapCoordinates(_ent, _ent);
        // Get all lights in range
        _lightQuery.Clear();
        _lookup.GetEntitiesInRange(transform, _ent.Comp1.DarkenRange, _lightQuery, flags: LookupFlags.StaticSundries);
        _system.ProcessLight(_ent, transform, _lightQuery, _ent);

        return null;
    }
}
