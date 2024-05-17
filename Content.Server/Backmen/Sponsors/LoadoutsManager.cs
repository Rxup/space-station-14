using System.Diagnostics.CodeAnalysis;
using Content.Corvax.Interfaces.Shared;
using Robust.Shared.Network;

namespace Content.Server.Backmen.Sponsors;

public sealed class LoadoutsManager : ISharedLoadoutsManager
{
    [Dependency] private readonly ISharedSponsorsManager _sponsorsManager = default!;

    public void Initialize()
    {
    }

    public bool TryGetServerPrototypes(NetUserId userId, [NotNullWhen(true)] out List<string>? prototypes)
    {
        if (!_sponsorsManager.TryGetLoadouts(userId, out prototypes))
        {
            prototypes = null;
            return false;
        }

        return true;
    }
}
