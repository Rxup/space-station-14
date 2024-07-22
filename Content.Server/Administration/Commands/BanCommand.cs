using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using Content.Server.Administration.Managers;
using Content.Shared.Administration;
using Content.Server.Database;
using Content.Server.Discord;
using Content.Server.GameTicking;
using Content.Shared.CCVar;
using Content.Shared.Database;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Console;
using Robust.Shared.Player;


namespace Content.Server.Administration.Commands;

[AdminCommand(AdminFlags.Ban)]
public sealed class BanCommand : LocalizedCommands
{

    [Dependency] private readonly IPlayerLocator _locator = default!;
    [Dependency] private readonly IBanManager _bans = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly ILogManager _logManager = default!;
    [Dependency] private readonly IServerDbManager _dbManager = default!;
    [Dependency] private readonly IEntitySystemManager _entitySystemManager = default!;

    public override string Command => "ban";

    public override async void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        string target;
        string reason;
        uint minutes;

        var webhookUrl = _cfg.GetCVar(CCVars.DiscordBansWebhook);
        var serverName = _cfg.GetCVar(CCVars.GameHostName);

        serverName = serverName[..Math.Min(serverName.Length, 1500)];

        var gameTicker = _entitySystemManager.GetEntitySystem<GameTicker>();
        var round = gameTicker.RunLevel switch
        {
            GameRunLevel.PreRoundLobby => gameTicker.RoundId == 0
                ? "pre-round lobby after server restart" // first round after server restart has ID == 0
                : $"pre-round lobby for round {gameTicker.RoundId + 1}",
            GameRunLevel.InRound => $"round {gameTicker.RoundId}",
            GameRunLevel.PostRound => $"post-round {gameTicker.RoundId}",
            _ => throw new ArgumentOutOfRangeException(nameof(gameTicker.RunLevel), $"{gameTicker.RunLevel} was not matched."),
        };

        if (!Enum.TryParse(_cfg.GetCVar(CCVars.ServerBanDefaultSeverity), out NoteSeverity severity))
        {
            _logManager.GetSawmill("admin.server_ban")
                .Warning("Server ban severity could not be parsed from config! Defaulting to high.");
            severity = NoteSeverity.High;
        }

        switch (args.Length)
        {
            case 2:
                target = args[0];
                reason = args[1];
                minutes = 0;
                break;
            case 3:
                target = args[0];
                reason = args[1];

                if (!uint.TryParse(args[2], out minutes))
                {
                    shell.WriteLine(Loc.GetString("cmd-ban-invalid-minutes", ("minutes", args[2])));
                    shell.WriteLine(Help);
                    return;
                }

                break;
            case 4:
                target = args[0];
                reason = args[1];

                if (!uint.TryParse(args[2], out minutes))
                {
                    shell.WriteLine(Loc.GetString("cmd-ban-invalid-minutes", ("minutes", args[2])));
                    shell.WriteLine(Help);
                    return;
                }

                if (!Enum.TryParse(args[3], ignoreCase: true, out severity))
                {
                    shell.WriteLine(Loc.GetString("cmd-ban-invalid-severity", ("severity", args[3])));
                    shell.WriteLine(Help);
                    return;
                }

                break;
            default:
                shell.WriteLine(Loc.GetString("cmd-ban-invalid-arguments"));
                shell.WriteLine(Help);
                return;
        }

        var located = await _locator.LookupIdByNameOrIdAsync(target);
        var player = shell.Player;

        if (located == null)
        {
            shell.WriteError(Loc.GetString("cmd-ban-player"));
            return;
        }

        var targetUid = located.UserId;
        var targetHWid = located.LastHWId;

        _bans.CreateServerBan(targetUid, target, player?.UserId, null, targetHWid, minutes, severity, reason);

        if (!string.IsNullOrEmpty(webhookUrl))
        {
            var banId = await _dbManager.GetLastServerBanId();

            DateTimeOffset? expires = null;
            if (minutes > 0)
            {
                expires = DateTimeOffset.Now + TimeSpan.FromMinutes(minutes);
            }

            var payload = new WebhookPayload
            {
                Username = "Это бан",
                AvatarUrl = "",
                Embeds = new List<WebhookEmbed>
                {
                    new WebhookEmbed
                    {
                        Color = 0xff0000,
                        Description = GenerateBanDescription(banId, target, shell.Player, minutes, reason, expires),
                        Footer = new WebhookEmbedFooter
                        {
                            Text = $"{serverName} ({round})"
                        }
                    }
                }
            };

            await _httpClient.PostAsync($"{webhookUrl}?wait=true",
                new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"));
        }
    
    }

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        if (args.Length == 1)
        {
            var options = _playerManager.Sessions.Select(c => c.Name).OrderBy(c => c).ToArray();
            return CompletionResult.FromHintOptions(options, LocalizationManager.GetString("cmd-ban-hint"));
        }

        if (args.Length == 2)
            return CompletionResult.FromHint(LocalizationManager.GetString("cmd-ban-hint-reason"));

        if (args.Length == 3)
        {
            var durations = new CompletionOption[]
            {
                new("0", LocalizationManager.GetString("cmd-ban-hint-duration-1")),
                new("1440", LocalizationManager.GetString("cmd-ban-hint-duration-2")),
                new("4320", LocalizationManager.GetString("cmd-ban-hint-duration-3")),
                new("10080", LocalizationManager.GetString("cmd-ban-hint-duration-4")),
                new("20160", LocalizationManager.GetString("cmd-ban-hint-duration-5")),
                new("43800", LocalizationManager.GetString("cmd-ban-hint-duration-6")),
            };

            return CompletionResult.FromHintOptions(durations, LocalizationManager.GetString("cmd-ban-hint-duration"));
        }

        if (args.Length == 4)
        {
            var severities = new CompletionOption[]
            {
                new("none", Loc.GetString("admin-note-editor-severity-none")),
                new("minor", Loc.GetString("admin-note-editor-severity-low")),
                new("medium", Loc.GetString("admin-note-editor-severity-medium")),
                new("high", Loc.GetString("admin-note-editor-severity-high")),
            };

            return CompletionResult.FromHintOptions(severities, Loc.GetString("cmd-ban-hint-severity"));
        }

        return CompletionResult.Empty;
    }

    private string GenerateBanDescription(int banId, string target, ICommonSession? player, uint minutes, string reason, DateTimeOffset? expires)
    {
        var builder = new StringBuilder();

        builder.AppendLine($"### **Бан | ID {banId}**");
        builder.AppendLine($"**Нарушитель:** *{target}*");
        builder.AppendLine($"**Причина:** {reason}");

        var banDuration = TimeSpan.FromMinutes(minutes);

        builder.Append($"**Длительность:** ");

        if (expires != null)
        {
            builder.Append($"{banDuration.Days} {NumWord(banDuration.Days, "день", "дня", "дней")}, ");
            builder.Append($"{banDuration.Hours} {NumWord(banDuration.Hours, "час", "часа", "часов")}, ");
            builder.AppendLine($"{banDuration.Minutes} {NumWord(banDuration.Minutes, "минута", "минуты", "минут")}");

        }
        else
        {
            builder.AppendLine($"***Навсегда***");
        }

        if (expires != null)
        {
            builder.AppendLine($"**Дата снятия наказания:** {expires}");
        }

        builder.Append($"**Наказание выдал(-а):** ");

        if (player != null)
        {
            builder.AppendLine($"*{player.Name}*");
        }
        else
        {
            builder.AppendLine($"***СИСТЕМА***");
        }

        return builder.ToString();
    }

    private string NumWord(int value, params string[] words)
    {
        value = Math.Abs(value) % 100;
        var num = value % 10;

        if (value > 10 && value < 20)
        {
            return words[2];
        }

        if (value > 1 && value < 5)
        {
            return words[1];
        }

        if (num == 1)
        {
            return words[0];
        }

        return words[2];
    }
}
