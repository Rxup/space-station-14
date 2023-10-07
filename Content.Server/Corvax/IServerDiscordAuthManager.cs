using System.Threading;
using System.Threading.Tasks;
using Content.Corvax.Interfaces.Shared;
using Content.Server.Backmen.DiscordAuth;
using Robust.Server.Player;
using Robust.Shared.Network;

namespace Content.Corvax.Interfaces.Server;

public interface IServerDiscordAuthManager : ISharedDiscordAuthManager
{
    public event EventHandler<IPlayerSession>? PlayerVerified;
    public Task<DiscordAuthManager.DiscordGenerateLinkResponse> GenerateAuthLink(NetUserId userId, CancellationToken cancel);
    public Task<bool> IsVerified(NetUserId userId, CancellationToken cancel);
}
