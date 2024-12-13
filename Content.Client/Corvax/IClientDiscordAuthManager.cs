using Content.Corvax.Interfaces.Shared;
using Robust.Client.Graphics;

namespace Content.Corvax.Interfaces.Client;

public interface IClientDiscordAuthManager : ISharedDiscordAuthManager
{
    public string AuthUrl { get; }
    public Texture? Qrcode { get; }
    public bool IsVerified { get; }
    void ByPass();
}
