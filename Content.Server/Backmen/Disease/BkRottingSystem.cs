using Content.Shared.Backmen.Disease;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Backmen.Disease;

public sealed partial class BkRottingSystem : SharedBkRottingSystem
{
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private IGameTiming _timing = default!;

    private readonly Dictionary<ProtoId<DiseasePoolPrototype>, DiseasePoolState> _pools = new();

    private sealed class DiseasePoolState
    {
        public required List<ProtoId<DiseasePrototype>> Diseases;
        public required TimeSpan RepickTime;
        public ProtoId<DiseasePrototype> Current;
        public TimeSpan NextRepick;
    }

    public override void Initialize()
    {
        base.Initialize();

        ReloadPools();
        _prototypes.PrototypesReloaded += OnPrototypesReloaded;
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _prototypes.PrototypesReloaded -= OnPrototypesReloaded;
    }

    private void OnPrototypesReloaded(PrototypesReloadedEventArgs args)
    {
        if (!args.WasModified<DiseasePoolPrototype>())
            return;

        ReloadPools();
    }

    private void ReloadPools()
    {
        var now = _timing.CurTime;
        var previous = new Dictionary<ProtoId<DiseasePoolPrototype>, ProtoId<DiseasePrototype>>(_pools.Count);
        foreach (var (id, state) in _pools)
        {
            previous[id] = state.Current;
        }

        _pools.Clear();

        foreach (var proto in _prototypes.EnumeratePrototypes<DiseasePoolPrototype>())
        {
            if (proto.Diseases.Count == 0)
            {
                Log.Error($"Disease pool {proto.ID} has no diseases.");
                continue;
            }

            ProtoId<DiseasePrototype> current;
            var poolId = (ProtoId<DiseasePoolPrototype>)proto.ID;
            if (previous.TryGetValue(poolId, out var oldCurrent) && proto.Diseases.Contains(oldCurrent))
                current = oldCurrent;
            else
                current = _random.Pick(proto.Diseases);

            _pools[poolId] = new DiseasePoolState
            {
                Diseases = new List<ProtoId<DiseasePrototype>>(proto.Diseases),
                RepickTime = proto.RepickTime,
                Current = current,
                NextRepick = now + proto.RepickTime,
            };
        }
    }

    public override ProtoId<DiseasePrototype>? GetCurrentPoolDisease(ProtoId<DiseasePoolPrototype> poolId = default)
    {
        if (poolId == default)
            poolId = MiasmaPool;

        if (!_pools.TryGetValue(poolId, out var pool))
            return null;

        return pool.Current;
    }

    public override ProtoId<DiseasePrototype>? RequestPoolDisease(ProtoId<DiseasePoolPrototype> poolId = default)
    {
        if (poolId == default)
            poolId = MiasmaPool;

        if (!_pools.TryGetValue(poolId, out var pool))
            return null;

        // Reset timer so the outbreak stays on one disease while people keep getting infected.
        pool.NextRepick = _timing.CurTime + pool.RepickTime;
        return pool.Current;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        foreach (var pool in _pools.Values)
        {
            if (now < pool.NextRepick)
                continue;

            pool.NextRepick = now + pool.RepickTime;
            pool.Current = _random.Pick(pool.Diseases);
        }
    }
}
