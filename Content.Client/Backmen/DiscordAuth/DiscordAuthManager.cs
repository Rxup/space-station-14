using System.IO;
using System.Threading;
using Content.Shared.Backmen.CCVar;
using Content.Shared.Backmen.DiscordAuth;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.State;
using Robust.Shared.Configuration;
using Robust.Shared.ContentPack;
using Robust.Shared.Network;
using Robust.Shared.Utility;
using Timer = Robust.Shared.Timing.Timer;

namespace Content.Client.Backmen.DiscordAuth;

public sealed class DiscordAuthManager : Content.Corvax.Interfaces.Client.IClientDiscordAuthManager
{
    [Dependency] private readonly IClientNetManager _netManager = default!;
    [Dependency] private readonly IStateManager _stateManager = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;

    public string AuthUrl { get; private set; } = string.Empty;
    public Texture? Qrcode { get; private set; }
    public bool IsVerified { get; private set; } = true;
    public bool IsOpt { get; private set; }
    public bool IsEnabled { get; private set; }

    public void Initialize()
    {
        _netManager.RegisterNetMessage<MsgDiscordAuthCheck>();
        _netManager.RegisterNetMessage<MsgDiscordAuthByPass>();
        _netManager.RegisterNetMessage<MsgDiscordAuthRequired>(OnDiscordAuthRequired);

        _cfg.OnValueChanged(CCVars.DiscordAuthIsOptional, v => IsOpt = v, true);
        _cfg.OnValueChanged(CCVars.DiscordAuthEnabled, v => IsEnabled = v, true);
    }

    private void OnDiscordAuthRequired(MsgDiscordAuthRequired message)
    {
        if (_stateManager.CurrentState is not DiscordAuthState)
        {
            AuthUrl = message.AuthUrl;
            if (message.QrCode.Length > 0)
            {
                using var ms = new MemoryStream(message.QrCode);
                Qrcode = Texture.LoadFromPNGStream(ms);
            }

            _stateManager.RequestStateChange<DiscordAuthState>();
        }
    }

    public void ByPass()
    {
        IsVerified = false;
        _netManager.ClientSendMessage(new MsgDiscordAuthByPass());
    }
}
