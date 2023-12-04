using System.Text.Json.Nodes;
using Content.Corvax.Interfaces.Server;
using Content.Shared.CCVar;
using Content.Shared.GameTicking;
using Robust.Server.ServerStatus;
using Robust.Shared.Configuration;

namespace Content.Server.GameTicking
{
    public sealed partial class GameTicker
    {
        /// <summary>
        ///     Used for thread safety, given <see cref="IStatusHost.OnStatusRequest"/> is called from another thread.
        /// </summary>
        private readonly object _statusShellLock = new();

        /// <summary>
        ///     Round start time in UTC, for status shell purposes.
        /// </summary>
        [ViewVariables]
        private DateTime _roundStartDateTime;

        /// <summary>
        ///     For access to CVars in status responses.
        /// </summary>
        [Dependency] private readonly IConfigurationManager _cfg = default!;

        // Corvax-Queue-Start
        [Dependency] private readonly IServerJoinQueueManager _joinQueueManager = default!;
        // Corvax-Queue-End

        /// <summary>
        ///     For access to the round ID in status responses.
        /// </summary>
        [Dependency] private readonly SharedGameTicker _gameTicker = default!;

        private void InitializeStatusShell()
        {
            IoCManager.Resolve<IStatusHost>().OnStatusRequest += GetStatusResponse;
        }

        private void GetStatusResponse(JsonNode jObject)
        {
            // This method is raised from another thread, so this better be thread safe!
            lock (_statusShellLock)
            {
                // Corvax-Queue-Start
                var players = _joinQueueManager.ActualPlayersCount;
                // Corvax-Queue-End

                jObject["name"] = _baseServer.ServerName;
                jObject["map"] = _gameMapManager.GetSelectedMap()?.MapName;
                jObject["round_id"] = _gameTicker.RoundId;
                // Corvax-Queue-Start
                jObject["players"] = players;
                // Corvax-Queue-End
                jObject["soft_max_players"] = _cfg.GetCVar(CCVars.SoftMaxPlayers);
                jObject["run_level"] = (int) _runLevel;
                if (_runLevel >= GameRunLevel.InRound)
                {
                    jObject["round_start_time"] = _roundStartDateTime.ToString("o");
                }
            }
        }
    }
}
