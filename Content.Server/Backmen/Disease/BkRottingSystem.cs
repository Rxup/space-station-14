using Content.Shared.Backmen.Disease;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Backmen.Disease;

public sealed class BkRottingSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    /// Miasma Disease Pool
    /// Miasma outbreaks are not per-entity,
    /// so this ensures that each entity in the same incident
    /// receives the same disease.

    public readonly List<ProtoId<DiseasePrototype>> MiasmaDiseasePool = new()
    {
        "VentCough",
        "AMIV",
        "SpaceCold",
        "SpaceFlu",
        "BirdFlew",
        "VanAusdallsRobovirus",
        "BleedersBite",
        "Plague",
        "TongueTwister",
        "MemeticAmirmir"
    };

    /// <summary>
    /// The current pool disease.
    /// </summary>
    private string _poolDisease = "";

    /// <summary>
    /// The target time it waits until..
    /// After that, it resets current time + _poolRepickTime.
    /// Any infection will also reset it to current time + _poolRepickTime.
    /// </summary>
    private TimeSpan _diseaseTime = TimeSpan.FromMinutes(5);

    /// <summary>
    /// How long without an infection before we pick a new disease.
    /// </summary>
    private readonly TimeSpan _poolRepickTime = TimeSpan.FromMinutes(5);


    public override void Initialize()
    {
        base.Initialize();

        // Init disease pool
        _poolDisease = _random.Pick(MiasmaDiseasePool);
    }

    public string RequestPoolDisease()
    {
        // We reset the current time on this outbreak so people don't get unlucky at the transition time
        _diseaseTime = _timing.CurTime + _poolRepickTime;
        return _poolDisease;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_timing.CurTime >= _diseaseTime)
        {
            _diseaseTime = _timing.CurTime + _poolRepickTime;
            _poolDisease = _random.Pick(MiasmaDiseasePool);
        }
    }
}
