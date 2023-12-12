using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Threading.Tasks;
using Content.Corvax.Interfaces.Shared;
using Robust.Shared.Maths;
using Robust.Shared.Network;

namespace Content.Corvax.Interfaces.Server;

public interface IServerSponsorsManager : ISharedSponsorsManager
{
    public bool TryGetGhostTheme(NetUserId userId, [NotNullWhen(true)] out string? ghostTheme);
    public bool TryGetPrototypes(NetUserId userId, [NotNullWhen(true)] out List<string>? prototypes);
    public bool TryGetOocColor(NetUserId userId, [NotNullWhen(true)] out Color? color);
    public int GetExtraCharSlots(NetUserId userId);
    public bool HavePriorityJoin(NetUserId userId);
}

public interface IServerVPNGuardManager
{
    public void Initialize();
    public Task<bool> IsConnectionVpn(IPAddress ip);
}
