using Content.Corvax.Interfaces.Shared;

namespace Content.Client.Backmen.Sponsors;

public sealed class LoadoutsManager : ISharedLoadoutsManager
{
    [Dependency] private readonly ISharedSponsorsManager _sponsorsManager = default!;

    public void Initialize()
    {
    }

    public List<string> GetClientPrototypes()
    {
        return _sponsorsManager.GetClientLoadouts();
    }
}
