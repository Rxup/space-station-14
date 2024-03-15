using System.Threading.Tasks;
using Content.Server.Database;
using Content.Server.GameTicking;
using Content.Server.Traits;
using Content.Shared.Backmen;
using Content.Shared.Backmen.WL;
using Content.Shared.CombatMode.Pacification;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Species.Components;
using JetBrains.Annotations;
using Robust.Server.Player;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server.Backmen.RoleWhitelist;

[UsedImplicitly]
public sealed class WhitelistSystem  : SharedWhitelistSystem
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

        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawnComplete, after: new []{ typeof(TraitSystem) });
    }

    [ValidatePrototypeId<SpeciesPrototype>]
    private const string SpecieDiona = "Diona";

    private void OnPlayerSpawnComplete(PlayerSpawnCompleteEvent msg)
    {
        if (!IsInWhitelist(msg.Player))
        {
            return;
        }

        if (msg.Profile.Species == SpecieDiona)
        {
            RemComp<PacifiedComponent>(msg.Mob);
        }
    }

    [ValidatePrototypeId<EntityPrototype>]
    private const string DionaReform = "MobDionaReformed";
    public override void ProcessReform(EntityUid child, Entity<ReformComponent> source)
    {
        ActorComponent? actor = null;
        if (!Resolve(child, ref actor, false))
        {
            if (!Resolve(source, ref actor, false))
            {
                return;
            }
        }

        if (!IsInWhitelist(actor.PlayerSession))
        {
            return;
        }

        if (source.Comp.ReformPrototype == DionaReform)
        {
            RemCompDeferred<PacifiedComponent>(child);
        }

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
    public bool IsInWhitelist(ICommonSession p)
    {
        return _whitelisted.Contains(p.UserId);
    }

    public void AddWhitelist(ICommonSession p)
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
    public void RemoveWhitelist(ICommonSession p)
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
    public void SendWhitelistCached(ICommonSession playerSession)
    {
        SendWhitelistCached(playerSession.Channel);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _net.Connecting -= NetOnConnecting;
        _net.Connected -= NetOnConnected;
    }
}
