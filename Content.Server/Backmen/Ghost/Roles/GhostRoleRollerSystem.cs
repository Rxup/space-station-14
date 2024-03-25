using System.Linq;
using System.Threading;
using Content.Server.Administration.Logs;
using Content.Server.Chat.Managers;
using Content.Server.GameTicking;
using Content.Server.Ghost.Roles;
using Content.Server.Ghost.Roles.Components;
using Content.Shared.Backmen.CCVar;
using Content.Shared.Backmen.Ghost.Roles;
using Content.Shared.Backmen.Ghost.Roles.Components;
using Content.Shared.Backmen.Ghost.Roles.Events;
using Content.Shared.Chat;
using Content.Shared.Database;
using Content.Shared.GameTicking;
using Content.Shared.Mind;
using Content.Shared.Players;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Random;
using Robust.Shared.Utility;
using Timer = Robust.Shared.Timing.Timer;

namespace Content.Server.Backmen.Ghost.Roles;

public sealed class GhostRoleRollerSystem : SharedGhostRoleRollerSystem
{
    private record TimerQueue(uint Id, Entity<GhostRoleComponent> Role, TimeSpan Start, TimeSpan DeadLine)
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
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IAdminLogManager _adminLogger = default!;
    [Dependency] private readonly GhostRoleSystem _ghostRoleSystem = default!;

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

        _playerManager.PlayerStatusChanged += PlayerManagerOnPlayerStatusChanged;

        SubscribeLocalEvent<GhostVisRollerComponent, PlayerAttachedEvent>(OnFirstSync);
        SubscribeNetworkEvent<CancelGhostRollerEvent>(OnCancelWait);
    }

    private void OnCancelWait(CancelGhostRollerEvent msg, EntitySessionEventArgs args)
    {
        if (!_busy.Contains(args.SenderSession.UserId))
            return;
        if (!_queues.TryGetValue(msg.Id, out var queue) || queue.IsFinished || queue.IsProcess || !queue.Bids.ContainsKey(args.SenderSession.UserId))
            return;

        _busy.Remove(args.SenderSession.UserId);
        if (queue.Bids[args.SenderSession.UserId] != 0f)
        {
            SendToAllPlayerInQueue(queue, Loc.GetString("ghostroller-notify-exit", ("owner",args.SenderSession.Name), ("roll", queue.Bids[args.SenderSession.UserId])));
        }
        queue.Bids[args.SenderSession.UserId] = -1;
        var ghost = GetBySession(args.SenderSession);
        if (ghost != null)
        {
            UiRemove(msg.Id,ghost.Value);
        }

        UpdateRollData(queue.Id);
    }

    private void UpdateRollData(uint id)
    {
        if (!_queues.TryGetValue(id, out var queue) || queue.IsFinished)
            return;

        var q = EntityQueryEnumerator<GhostVisRollerComponent>();
        while (q.MoveNext(out var owner, out var ghostRoller))
        {
            if(ghostRoller.CurrentId != id)
                continue;

            UpdateRollData(id, (owner, ghostRoller));
        }
    }

    private void UpdateRollData(uint id, Entity<GhostVisRollerComponent> ent)
    {
        if (!_queues.TryGetValue(id, out var queue) || queue.IsFinished)
            return;

        ent.Comp.Bids =
            queue.Bids.ToDictionary(
                x => _playerManager.TryGetSessionById(x.Key, out var sess) ? sess.Name : x.Key.ToString(),
                x => x.Value);
        ent.Comp.StartDate = queue.Start;
        if (!TerminatingOrDeleted(queue.Role))
        {
            ent.Comp.Title = queue.Role.Comp.RoleName;
            ent.Comp.Desc = queue.Role.Comp.RoleDescription;
            ent.Comp.Rule = queue.Role.Comp.RoleRules;
        }
        else
        {
            ent.Comp.Title = null;
            ent.Comp.Desc = null;
            ent.Comp.Rule = null;
        }
        Dirty(ent);
    }

    private void OnFirstSync(Entity<GhostVisRollerComponent> ent, ref PlayerAttachedEvent args)
    {
        if (!_busy.Contains(args.Player.UserId) || !_history.TryGetValue(args.Player.UserId, out var playerHistory))
            return;
        var table = playerHistory.LastOrDefault();
        if(table == null)
            return;
        ent.Comp.Bids = table.Bids.ToDictionary(x => _playerManager.TryGetSessionById(x.Key, out var sess) ? sess.Name : x.Key.ToString(), x => x.Value);
        ent.Comp.StartDate = table.Start;
        ent.Comp.CurrentId = table.Id;
        if (TryComp<GhostRoleComponent>(ent, out var ghostRoleComponent))
        {
            ent.Comp.Title = ghostRoleComponent.RoleName;
            ent.Comp.Desc = ghostRoleComponent.RoleDescription;
            ent.Comp.Rule = ghostRoleComponent.RoleRules;
        }
        Dirty(ent);
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
            if(!_busy.Contains(e.Session.UserId))
                return;
            _busy.Remove(e.Session.UserId);
            foreach (var (id, queue) in _queues)
            {
                if (queue.IsFinished || queue.IsProcess || !queue.Bids.ContainsKey(e.Session.UserId))
                {
                    continue;
                }

                queue.Bids[e.Session.UserId] = -1;
                UpdateRollData(id);
            }
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
        var q = new TimerQueue(role.Comp.Identifier, role, now, now + _roleTimer + TimeSpan.FromSeconds(1));
        if (_queues.TryAdd(role.Comp.Identifier, q))
            Timer.Spawn(_roleTimer, () => EndRoleTimer(q.Id), q.TokenSource.Token);
    }

    public void UnregisterGhostRole(Entity<GhostRoleComponent> role)
    {
        if(!_enabled)
            return;

        UiRemove(role.Comp.Identifier);
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

        var bid = 0;

        if (queue.Bids.ContainsKey(ev.Player.UserId) && queue.Bids[ev.Player.UserId] <= -1f)
        {
            bid = 0;
        }
        else
        {
            bid = _random.Next(1, 100);
        }

        if (!queue.Bids.TryAdd(ev.Player.UserId, bid))
        {
            queue.Bids[ev.Player.UserId] = bid;
        }
        _history.TryAdd(ev.Player.UserId, new List<TimerQueue>());
        _history[ev.Player.UserId].Add(queue);
        UiAddToQuery(role.Comp.Identifier, role, ev.Player, bid);

        _chatManager.DispatchServerMessage(ev.Player, Loc.GetString(bid == 0f ? "ghostroller-notify-bid-penalty" : "ghostroller-notify-bid",("entity",role.Owner),("roll",bid)), true);
    }

    private void UiRemove(uint compIdentifier)
    {
        var q = EntityQueryEnumerator<GhostVisRollerComponent>();
        while (q.MoveNext(out var owner, out var ghostRoller))
        {
            if(ghostRoller.CurrentId != compIdentifier)
                continue;
            UiRemove(compIdentifier, (owner, ghostRoller));
        }
    }
    private void UiRemove(uint compIdentifier, Entity<GhostVisRollerComponent> ghostRoller)
    {
        ghostRoller.Comp.CurrentId = 0;
        ghostRoller.Comp.Bids.Clear();
        ghostRoller.Comp.StartDate = TimeSpan.Zero;
        ghostRoller.Comp.Title = "";
        ghostRoller.Comp.Desc = "";
        ghostRoller.Comp.Rule = "";
        Dirty(ghostRoller);
    }

    private Entity<GhostVisRollerComponent>? GetBySession(ICommonSession sess)
    {
        var mindId = sess.GetMind();
        var mind = CompOrNull<MindComponent>(mindId);
        if (mind == null)
            return null;

        var ghost = mind.IsVisitingEntity ? mind.VisitingEntity : sess.AttachedEntity;
        if (!TryComp<GhostVisRollerComponent>(ghost, out var ghostPlayer))
            return null;

        return (ghost.Value, ghostPlayer);
    }
    private void UiAddToQuery(uint compIdentifier, Entity<GhostRoleComponent> role, ICommonSession evPlayer, int bid)
    {
        var ghost = GetBySession(evPlayer);
        if (
            ghost == null ||
            !_queues.TryGetValue(compIdentifier, out var queue)
            )
            return;

        ghost.Value.Comp.CurrentId = compIdentifier;
        UpdateRollData(compIdentifier);
        _ghostRoleSystem.CloseEui(evPlayer);
    }

    private void EndRoleTimer(uint id)
    {
        if(!_enabled)
            return;

        if (!_queues.TryGetValue(id, out var queue))
        {
            Log.Error($"queue {id} not exist!");
            return;
        }

        var role = queue.Role;

        try
        {
            queue.IsProcess = true;

            if (TerminatingOrDeleted(role) || queue.TokenSource.Token.IsCancellationRequested)
            {
                Log.Error($"queue {role.Comp.Identifier} delete or canceled!");
                if (queue.Bids.Count > 0)
                {
                    SendToAllPlayerInQueue(queue, Loc.GetString("ghostroller-notify-canceled"));
                }

                queue.IsFinished = true;
                return;
            }

            var now = _gameTicker.RoundDuration();

            startpick:
            var bids = queue.Bids.Where(x => x.Value >= 0).ToArray();
            if (bids.Length == 0)
            {
                Log.Error($"queue {role.Comp.Identifier} no binds!");
                queue.IsFinished = true;
                return;
            }

            var winner = bids.MaxBy(x => x.Value);

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
            UiRemove(role.Comp.Identifier);
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
        foreach (var (userId, bid) in queue.Bids)
        {
            if(bid <= 0f)
                continue;
            if(!_playerManager.TryGetSessionById(userId, out var sess))
                continue;
            filter.AddPlayer(sess);
            _ghostRoleSystem.CloseEui(sess);
        }
        var wrappedMessage = Loc.GetString("chat-manager-server-wrap-message", ("message", FormattedMessage.EscapeText(msg)));
        _chatManager.ChatMessageToManyFiltered( filter, ChatChannel.Server, msg, wrappedMessage, default, false, true, null);
    }

}
