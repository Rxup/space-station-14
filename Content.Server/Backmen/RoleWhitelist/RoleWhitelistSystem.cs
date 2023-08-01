using Content.Server.Ghost.Roles;
using Content.Server.Ghost.Roles.Components;
using Content.Server.Players;
using Content.Shared.Backmen;
using Content.Shared.Backmen.CCVar;
using JetBrains.Annotations;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Network;

namespace Content.Server.Backmen.RoleWhitelist;

[UsedImplicitly]
public sealed class RoleWhitelistSystem  : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IServerNetManager _net = default!;

    public override void Initialize()
    {
        base.Initialize();

        _net.RegisterNetMessage<MsgWhitelist>();
    }


    public void SendWhitelistCached(IPlayerSession playerSession)
    {
        var whitelist = playerSession.ContentData()?.Whitelisted ?? false;

        var msg = new MsgWhitelist
        {
            Whitelisted = whitelist
        };

        _net.ServerSendMessage(msg, playerSession.ConnectedClient);
    }
}
