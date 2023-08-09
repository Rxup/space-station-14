﻿using System.Threading.Tasks;
using Content.Server.Database;
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
public sealed class WhitelistSystem  : EntitySystem
{
    [Dependency] private readonly IServerNetManager _net = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IServerDbManager _db = default!;

    private readonly HashSet<NetUserId> _whitelisted = new();

    public override void Initialize()
    {
        base.Initialize();

        _net.RegisterNetMessage<MsgWhitelist>();

        _net.Connecting += NetOnConnecting;
        _net.Connected += NetOnConnected;
    }

    private void NetOnConnected(object? sender, NetChannelArgs e)
    {
        SendWhitelistCached(e.Channel.UserId);
    }

    private async Task NetOnConnecting(NetConnectingArgs session)
    {
        if (await _db.GetWhitelistStatusAsync(session.UserId))
        {
            _whitelisted.Add(session.UserId);
        }
        else
        {
            _whitelisted.Remove(session.UserId);
        }
    }

    public bool IsInWhitelist(NetUserId p)
    {
        return _whitelisted.Contains(p);
    }
    public bool IsInWhitelist(IPlayerSession p)
    {
        return _whitelisted.Contains(p.UserId);
    }

    public void AddWhitelist(IPlayerSession p)
    {
        if (_whitelisted.Add(p.UserId))
        {
            SendWhitelistCached(p);
        }
    }
    public void AddWhitelist(NetUserId p)
    {
        if (_whitelisted.Add(p))
        {
            SendWhitelistCached(p);
        }
    }
    public void RemoveWhitelist(IPlayerSession p)
    {
        if (_whitelisted.Remove(p.UserId))
        {
            SendWhitelistCached(p);
        }
    }
    public void RemoveWhitelist(NetUserId p)
    {
        if (_whitelisted.Remove(p))
        {
            SendWhitelistCached(p);
        }
    }

    public void SendWhitelistCached(INetChannel playerSession)
    {
        playerSession.SendMessage(new MsgWhitelist
        {
            Whitelisted = _whitelisted.Contains(playerSession.UserId)
        });
    }
    public void SendWhitelistCached(NetUserId playerSession)
    {
        if (_playerManager.TryGetSessionById(playerSession, out var p))
        {
            SendWhitelistCached(p);
        }
    }
    public void SendWhitelistCached(IPlayerSession playerSession)
    {
        SendWhitelistCached(playerSession.ConnectedClient);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _net.Connecting -= NetOnConnecting;
        _net.Connected -= NetOnConnected;
    }
}
