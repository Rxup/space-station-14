using Content.Shared.Mobs.Systems;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Shared.Backmen.Surgery.Consciousness.Systems;

[Virtual]
public partial class ConsciousnessSystem : EntitySystem
{
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly MobStateSystem _mobStateSystem = default!;

    [Dependency] private readonly IGameTiming _timing = default!;

    private const string UnspecifiedIdentifier = "Unspecified";

    private ISawmill _sawmill = default!;

    public override void Initialize()
    {
        base.Initialize();

        _sawmill = Logger.GetSawmill("consciousness");

        InitProcess();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        UpdatePassedOut(frameTime);
    }
}
