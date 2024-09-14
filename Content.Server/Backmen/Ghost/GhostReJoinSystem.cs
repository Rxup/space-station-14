using Content.Server.Chat.Managers;
using Content.Server.GameTicking;
using Content.Shared.Administration;
using Content.Shared.Administration.Logs;
using Content.Shared.Backmen.CCVar;
using Content.Shared.Backmen.Ghost;
using Content.Shared.Database;
using Content.Shared.GameTicking;
using Content.Shared.Ghost;
using Robust.Server.Player;
using Robust.Shared.Console;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server.Backmen.Ghost;

public sealed class GhostReJoinSystem : SharedGhostReJoinSystem
{
    [Dependency] private readonly ISharedAdminLogManager _adminLogger = default!;

    [Dependency] private readonly GameTicker _gameTicker = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IChatManager _chatManager = default!;
    [Dependency] private readonly IConsoleHost _console = default!;
    [Dependency] private readonly SharedGhostSystem _ghostSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RoundRestartCleanupEvent>(ResetDeathTimes);

        _configurationManager.OnValueChanged(CCVars.GhostRespawnMaxPlayers,
            ghostRespawnMaxPlayers =>
            {
                _ghostRespawnMaxPlayers = ghostRespawnMaxPlayers;
            },
            true);


        _console.RegisterCommand("bkm_return_to_round", ReturnToRoundCommand, ReturnToRoundCompletion);
    }

    private CompletionResult ReturnToRoundCompletion(IConsoleShell shell, string[] args)
    {
        return CompletionResult.Empty;
    }

    [AnyCommand]
    private void ReturnToRoundCommand(IConsoleShell shell, string argstr, string[] args)
    {
        if (shell.Player?.AttachedEntity is not { } entity || !TryComp<GhostComponent>(entity, out var ghostComponent))
        {
            shell.WriteError("This command can only be ran by a player with an attached entity.");
            return;
        }

        if (_playerManager.PlayerCount >= _ghostRespawnMaxPlayers)
        {
            SendChatMsg(shell.Player,
                Loc.GetString("ghost-respawn-max-players", ("players", _ghostRespawnMaxPlayers))
            );
            return;
        }

        var userId = shell.Player.UserId;

        if (!_deathTime.TryGetValue(userId, out var deathTime))
        {
            _deathTime[userId] = ghostComponent.TimeOfDeath;
        }

        var timeOffset = _gameTiming.CurTime - deathTime;

        if (timeOffset >= _ghostRespawnTime)
        {
            _deathTime.Remove(userId);

            _adminLogger.Add(LogType.Mind,
                LogImpact.Extreme,
                $"{shell.Player.Channel.UserName} вернулся в лобби посредством гост респавна.");

            SendChatMsg(shell.Player,
                Loc.GetString("ghost-respawn-window-rules-footer")
            );
            _gameTicker.Respawn(shell.Player);
            return;
        }

        SendChatMsg(shell.Player,
            Loc.GetString("ghost-respawn-time-left", ("time", (_ghostRespawnTime - timeOffset).ToString()))
        );
    }

    private int _ghostRespawnMaxPlayers;
    private readonly Dictionary<NetUserId, TimeSpan> _deathTime = new();

    private void ResetDeathTimes(RoundRestartCleanupEvent ev)
    {
        _deathTime.Clear();
    }

    private void SendChatMsg(ICommonSession sess, string message)
    {
        _chatManager.ChatMessageToOne(Shared.Chat.ChatChannel.Server,
            message,
            Loc.GetString("chat-manager-server-wrap-message", ("message", message)),
            default,
            false,
            sess.Channel,
            Color.Red);
    }

    public void AttachGhost(EntityUid? ghost, ICommonSession? mindSession)
    {
        if(mindSession == null)
            return;

        if(!_deathTime.ContainsKey(mindSession.UserId))
            _deathTime[mindSession.UserId] = _gameTiming.CurTime;

        Log.Debug($"Attach time {_deathTime[mindSession.UserId]} to ghost {ghost:entity}");

        if (TryComp<GhostComponent>(ghost, out var ghostComponent))
        {
            _ghostSystem.SetTimeOfDeath(ghost.Value, _deathTime[mindSession.UserId], ghostComponent);
            Dirty(ghost.Value, ghostComponent);
        }
    }
}
