using System.Linq;
using Content.Server.Administration;
using Content.Shared.Administration;
using Content.Shared.Mind;
using Content.Shared.Objectives.Components;
using Content.Shared.Objectives.Systems;
using Robust.Server.Player;
using Robust.Shared.Console;
using Robust.Shared.Player;

namespace Content.Server.Objectives.Commands
{
    [AdminCommand(AdminFlags.Logs)]
    public sealed class ListObjectivesCommand : LocalizedCommands
    {
        [Dependency] private readonly IEntityManager _entities = default!;
        [Dependency] private readonly IPlayerManager _players = default!;

        public override string Command => "lsobjectives";

        public override void Execute(IConsoleShell shell, string argStr, string[] args)
        {
            ICommonSession? player;
            if (args.Length > 0)
                _players.TryGetSessionByUsername(args[0], out player);
            else
                player = shell.Player;

            if (player == null)
            {
                shell.WriteError(LocalizationManager.GetString("shell-target-player-does-not-exist"));
                return;
            }

            var minds = _entities.System<SharedMindSystem>();
            if (!minds.TryGetMind(player, out var mindId, out var mind))
            {
                shell.WriteError(LocalizationManager.GetString("shell-target-entity-does-not-have-message", ("missing", "mind")));
                return;
            }

            shell.WriteLine($"Objectives for player {player.UserId}:");
            var objectivesGr = mind.Objectives.ToList()
                .Select(x=> (Entity<ObjectiveComponent?>)(x,_entities.GetComponentOrNull<ObjectiveComponent>(x)))
                .GroupBy(x=>x.Comp?.LocIssuer ?? "") //backmen: locale
                .ToList();
            if (objectivesGr.Count == 0)
            {
                shell.WriteLine("None.");
            }

            var objectivesSystem = _entities.System<SharedObjectivesSystem>();
            // start-backmen: locale
            foreach (var objective in objectivesGr)
            {
                var objectives = objective.ToList();
                shell.WriteMarkup(objective.Key+":");
                for (var i = 0; i < objectives.Count; i++)
                {
                    var info = objectivesSystem.GetInfo(objectives[i], mindId, mind);
                    if (info == null)
                    {
                        shell.WriteLine($"- [{i}] {objectives[i].Owner} - INVALID");
                    }
                    else
                    {
                        var progress = (int) (info.Value.Progress * 100f);
                        shell.WriteLine($"- [{i}] {objectives[i].Owner} ({info.Value.Title}) ({progress}%)");
                    }
                }
            }
            // end-backmen: locale
        }

        public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
        {
            if (args.Length == 1)
            {
                return CompletionResult.FromHintOptions(CompletionHelper.SessionNames(), LocalizationManager.GetString("shell-argument-username-hint"));
            }

            return CompletionResult.Empty;
        }
    }
}
