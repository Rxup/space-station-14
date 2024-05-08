﻿using System.Linq;
using Content.Server.Connection;
using Content.Server.GameTicking;
using Content.Shared.Backmen.JoinQueue;
using Content.Shared.CCVar;
using Content.Shared.GameTicking;
using Prometheus;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;
using Robust.Shared.Network;
using Robust.Shared.Player;

namespace Content.Server.Backmen.JoinQueue;



public sealed class JoinQueueManager : Content.Corvax.Interfaces.Server.IServerJoinQueueManager
{
    private static readonly Gauge QueueCount = Metrics.CreateGauge(
        "join_queue_count",
        "Amount of players in queue.");

    private static readonly Counter QueueBypassCount = Metrics.CreateCounter(
        "join_queue_bypass_count",
        "Amount of players who bypassed queue by privileges.");

    private static readonly Histogram QueueTimings = Metrics.CreateHistogram(
        "join_queue_timings",
        "Timings of players in queue",
        new HistogramConfiguration()
        {
            LabelNames = new[] {"type"},
            Buckets = Histogram.ExponentialBuckets(1, 2, 14),
        });

    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly IConnectionManager _connectionManager = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IServerNetManager _netManager = default!;
    [Dependency] private readonly Content.Corvax.Interfaces.Server.IServerDiscordAuthManager _discordAuthManager = default!;

    /// <summary>
    ///     Queue of active player sessions
    /// </summary>
    private readonly List<ICommonSession> _queue = new(); // Real Queue class can't delete disconnected users

    private bool _isIsEnabled = false;

    public bool IsEnabled => _isIsEnabled;
    public int PlayerInQueueCount => _queue.Count;
    public int ActualPlayersCount => _playerManager.PlayerCount - PlayerInQueueCount; // Now it's only real value with actual players count that in game

    public void Initialize()
    {
        _netManager.RegisterNetMessage<MsgQueueUpdate>();

        _cfg.OnValueChanged(Shared.Backmen.CCVar.CCVars.QueueEnabled, OnQueueCVarChanged, true);
        _cfg.OnValueChanged(CCVars.SoftMaxPlayers, OnSoftMaxPlayerChanged, true);
        _playerManager.PlayerStatusChanged += OnPlayerStatusChanged;
        _discordAuthManager.PlayerVerified += OnPlayerVerified;
    }

    private int _softMaxPlayers = 30;

    private void OnSoftMaxPlayerChanged(int val)
    {
        _softMaxPlayers = val;
        ProcessQueue(false, DateTime.Now);
    }

    public void PostInitialize()
    {

    }

    private void OnQueueCVarChanged(bool value)
    {
        _isIsEnabled = value;

        if (!value)
        {
            foreach (var session in _queue)
            {
                session.Channel.Disconnect("Queue was disabled");
            }
        }
    }

    private async void OnPlayerVerified(object? sender, ICommonSession session)
    {
        if (!_isIsEnabled)
        {
            SendToGame(session);
            return;
        }

        var isPrivileged = await _connectionManager.HavePrivilegedJoin(session.UserId);
        var currentOnline = _playerManager.PlayerCount - 1; // Do not count current session in general online, because we are still deciding her fate
        var haveFreeSlot = currentOnline < _softMaxPlayers;

        var wasInGame = _entityManager.TrySystem<GameTicker>(out var ticker) &&
                        ticker.PlayerGameStatuses.TryGetValue(session.UserId, out var status) &&
                        status == PlayerGameStatus.JoinedGame;

        if (isPrivileged || haveFreeSlot || wasInGame)
        {
            SendToGame(session);

            if (isPrivileged && !haveFreeSlot)
                QueueBypassCount.Inc();

            return;
        }

        _queue.Add(session);
        ProcessQueue(false, session.ConnectedTime);
    }

    private async void OnPlayerStatusChanged(object? sender, SessionStatusEventArgs e)
    {
        if (e.NewStatus == SessionStatus.Disconnected)
        {
            var wasInQueue = _queue.Remove(e.Session);

            if (!wasInQueue && e.OldStatus != SessionStatus.InGame) // Process queue only if player disconnected from InGame or from queue
                return;

            ProcessQueue(true, e.Session.ConnectedTime);

            if (wasInQueue)
                QueueTimings.WithLabels("Unwaited").Observe((DateTime.UtcNow - e.Session.ConnectedTime).TotalSeconds);
        }
    }

    /// <summary>
    ///     If possible, takes the first player in the queue and sends him into the game
    /// </summary>
    /// <param name="isDisconnect">Is method called on disconnect event</param>
    /// <param name="connectedTime">Session connected time for histogram metrics</param>
    private void ProcessQueue(bool isDisconnect, DateTime connectedTime)
    {
        var players = ActualPlayersCount;
        if (isDisconnect)
            players--; // Decrease currently disconnected session but that has not yet been deleted

        var haveFreeSlot = players < _softMaxPlayers;
        var queueContains = _queue.Count > 0;
        if (haveFreeSlot && queueContains)
        {
            var session = _queue.First();
            _queue.Remove(session);

            SendToGame(session);

            QueueTimings.WithLabels("Waited").Observe((DateTime.UtcNow - connectedTime).TotalSeconds);
        }

        SendUpdateMessages();
        QueueCount.Set(_queue.Count);
    }

    /// <summary>
    ///     Sends messages to all players in the queue with the current state of the queue
    /// </summary>
    private void SendUpdateMessages()
    {
        for (var i = 0; i < _queue.Count; i++)
        {
            _queue[i].Channel.SendMessage(new MsgQueueUpdate
            {
                Total = _queue.Count,
                Position = i + 1,
            });
        }
    }

    /// <summary>
    ///     Letting player's session into game, change player state
    /// </summary>
    /// <param name="s">Player session that will be sent to game</param>
    private void SendToGame(ICommonSession s)
    {
        _entityManager.System<PlayerManagerSystem>().JoinGame(s);
    }
}
