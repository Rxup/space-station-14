using System.Linq;
using System.Threading;
using Content.Server.Administration.Logs;
using Content.Server.Chat.Managers;
using Content.Server.GameTicking;
using Content.Server.Ghost.Roles;
using Content.Server.Ghost.Roles.Components;
using Content.Shared.Backmen.CCVar;
using Content.Shared.Chat;
using Content.Shared.Database;
using Content.Shared.GameTicking;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Random;
using Robust.Shared.Utility;
using Timer = Robust.Shared.Timing.Timer;

namespace Content.Server.Backmen.Ghost.Roles;

public sealed class GhostRoleRollerSystem : EntitySystem
{
    private record TimerQueue(TimeSpan Start, TimeSpan DeadLine)
    {
        public bool IsFinished { get; set; } = false;
        public bool IsProcess { get; set; } = false;
        public TimeSpan? FinishDate { get; set; }
        public NetUserId? FinishUser { get; set; }
        public Dictionary<NetUserId,float> Bids { get; } = new();
        public CancellationTokenSource TokenSource { get; } = new();
    }

    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IChatManager _chatManager = default!;
    [Dependency] private readonly GameTicker _gameTicker = default!;
    [Dependency] private readonly ILogManager _logManager = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IAdminLogManager _adminLogger = default!;
    [Dependency] private readonly GhostRoleSystem _ghostRoleSystem = default!;

    private ISawmill _sawmill = default!;

    private readonly Dictionary<uint, TimerQueue> _queues = new();
    private readonly Dictionary<NetUserId, List<TimerQueue>> _history = new();
    private readonly HashSet<NetUserId> _busy = new();

    private bool _enabled = false;
    private TimeSpan _roleTimer = TimeSpan.FromSeconds(3);
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnCleanup);
        _cfg.OnValueChanged(CCVars.GhostRollerTime, f =>
        {
            _roleTimer = TimeSpan.FromSeconds(f);
        }, true);

        _cfg.OnValueChanged(CCVars.GhostRollerEnabled, f =>
        {
            _enabled = f;
            if(!f)
                Cleanup();
        }, true);

        _sawmill = _logManager.GetSawmill("backmen.ghost");

        _playerManager.PlayerStatusChanged += PlayerManagerOnPlayerStatusChanged;
    }

    public override void Shutdown()
    {
        base.Shutdown();

        _playerManager.PlayerStatusChanged -= PlayerManagerOnPlayerStatusChanged;
    }

    private void PlayerManagerOnPlayerStatusChanged(object? sender, SessionStatusEventArgs e)
    {
        if (e.NewStatus is SessionStatus.Disconnected or SessionStatus.Connected)
        {
            _busy.Remove(e.Session.UserId);
        }
    }

    private void OnCleanup(RoundRestartCleanupEvent ev)
    {
        Cleanup();
    }

    private void Cleanup()
    {
        _queues.Clear();
        _history.Clear();
        _busy.Clear();
    }

    public void RegisterGhostRole(Entity<GhostRoleComponent> role)
    {
        if(!_enabled)
            return;

        var now = _gameTicker.RoundDuration();
        var q = new TimerQueue(now, now + _roleTimer + TimeSpan.FromSeconds(1));
        if(_queues.TryAdd(role.Comp.Identifier, q))
            Timer.Spawn(_roleTimer, () => EndRoleTimer(role), q.TokenSource.Token);
    }

    public void UnregisterGhostRole(Entity<GhostRoleComponent> role)
    {
        if(!_enabled)
            return;

        if (!_queues.TryGetValue(role.Comp.Identifier, out var queue) || queue.IsFinished)
            return;
        foreach (var (user, _) in queue.Bids)
        {
            _busy.Remove(user);
        }
        queue.TokenSource.Cancel(false);
    }

    public void Takeover(Entity<GhostRoleComponent> role, ref TakeGhostRoleEvent ev)
    {
        if(!_enabled)
            return;

        var now = _gameTicker.RoundDuration();

        if (_busy.Contains(ev.Player.UserId))
        {
            _chatManager.DispatchServerMessage(ev.Player, Loc.GetString("ghostroller-busy"), true);
            ev.TookRole = true;
            return;
        }

        if (TerminatingOrDeleted(role) ||
            !_queues.TryGetValue(role.Comp.Identifier, out var queue) ||
            queue.IsFinished ||
            queue.DeadLine < now ||
            queue.TokenSource.Token.IsCancellationRequested)
            return;

        if (queue.IsProcess)
        {
            _chatManager.DispatchServerMessage(ev.Player, Loc.GetString("ghostroller-is-process"), true);
            ev.TookRole = true;
            return;
        }

        ev.TookRole = true;

        _busy.Add(ev.Player.UserId);
        var bid = _random.Next(0, 100);
        queue.Bids.TryAdd(ev.Player.UserId, bid);
        _history.TryAdd(ev.Player.UserId, new List<TimerQueue>());
        _history[ev.Player.UserId].Add(queue);
        _chatManager.DispatchServerMessage(ev.Player, Loc.GetString("ghostroller-notify-bid",("entity",role.Owner),("roll",bid)), true);
    }

    private void EndRoleTimer(Entity<GhostRoleComponent> role)
    {
        if(!_enabled)
            return;

        if (!_queues.TryGetValue(role.Comp.Identifier, out var queue))
        {
            _sawmill.Error($"queue {role.Comp.Identifier} not exist!");
            return;
        }

        try
        {
            queue.IsProcess = true;

            if (TerminatingOrDeleted(role) || queue.TokenSource.Token.IsCancellationRequested)
            {
                _sawmill.Error($"queue {role.Comp.Identifier} delete or canceled!");
                if (queue.Bids.Count > 0)
                {
                    SendToAllPlayerInQueue(queue, Loc.GetString("ghostroller-notify-canceled"));
                }

                queue.IsFinished = true;
                return;
            }

            var now = _gameTicker.RoundDuration();

            startpick:
            if (queue.Bids.Count == 0)
            {
                _sawmill.Error($"queue {role.Comp.Identifier} no binds!");
                queue.IsFinished = true;
                return;
            }

            var winner = queue.Bids.MaxBy(x => x.Value);

            if (_playerManager.TryGetSessionById(winner.Key, out var player))
            {


                queue.FinishDate = now;
                queue.FinishUser = winner.Key;

                var ev = new TakeGhostRoleEvent(player);
                RaiseLocalEvent(role, ref ev);

                if (!ev.TookRole)
                {
                    SendToAllPlayerInQueue(queue, Loc.GetString("ghostroller-cant-be-took"));
                    return;
                }


                if (player.AttachedEntity != null)
                    _adminLogger.Add(LogType.GhostRoleTaken, LogImpact.Low,
                        $"{player:player} took the {role.Comp.RoleName:roleName} ghost role {ToPrettyString(player.AttachedEntity.Value):entity} with roll {winner.Value}");

                SendToAllPlayerInQueue(queue,
                    Loc.GetString("ghostroller-notify-winner", ("name", player.Name), ("entity", role.Owner), ("roll", winner.Value)), true);
            }
            else
            {
                queue.Bids.Remove(winner.Key);
                goto startpick; //repick
            }

            queue.IsFinished = true;
        }
        finally
        {
            queue.IsProcess = false;
            foreach (var (user, _) in queue.Bids)
            {
                _busy.Remove(user);
            }
        }
    }

    private void SendToAllPlayerInQueue(TimerQueue queue, string msg, bool cloneBUi = false)
    {
        var filter = Filter.Empty();
        foreach (var (userId, _) in queue.Bids)
        {
            if(!_playerManager.TryGetSessionById(userId, out var sess))
                continue;
            filter.AddPlayer(sess);
            _ghostRoleSystem.CloseEui(sess);
        }
        var wrappedMessage = Loc.GetString("chat-manager-server-wrap-message", ("message", FormattedMessage.EscapeText(msg)));
        _chatManager.ChatMessageToManyFiltered( filter, ChatChannel.Server, msg, wrappedMessage, default, false, true, null);
    }

}
