using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using Content.Server.Administration.Managers;
using Content.Server.Database;
using Content.Server.Discord;
using Content.Server.GameTicking;
using Content.Shared.Administration;
using Content.Shared.CCVar;
using Content.Shared.Database;
using Content.Shared.Roles;
using Robust.Shared.Configuration;
using Robust.Shared.Console;
using Robust.Shared.Player;

namespace Content.Server.Administration.Commands;

[AdminCommand(AdminFlags.Ban)]
public sealed class RoleBanCommand : IConsoleCommand
{
    [Dependency] private readonly IPlayerLocator _locator = default!;
    [Dependency] private readonly IBanManager _bans = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IServerDbManager _dbManager = default!;
    [Dependency] private readonly IEntitySystemManager _entitySystemManager = default!;
    private readonly HttpClient _httpClient = new();

    public string Command => "roleban";
    public string Description => Loc.GetString("cmd-roleban-desc");
    public string Help => Loc.GetString("cmd-roleban-help");
    string webhookUrl = _cfg.GetCVar(CCVars.DiscordBansWebhook);
    string  serverName = _cfg.GetCVar(CCVars.GameHostName);

    public async void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        string target;
        string job;
        string reason;
        uint minutes;
        if (!Enum.TryParse(_cfg.GetCVar(CCVars.RoleBanDefaultSeverity), out NoteSeverity severity))
        {
            Logger.WarningS("admin.role_ban", "Role ban severity could not be parsed from config! Defaulting to medium.");
            severity = NoteSeverity.Medium;
        }

        switch (args.Length)
        {
            case 3:
                target = args[0];
                job = args[1];
                reason = args[2];
                minutes = 0;
                break;
            case 4:
                target = args[0];
                job = args[1];
                reason = args[2];

                if (!uint.TryParse(args[3], out minutes))
                {
                    shell.WriteError(Loc.GetString("cmd-roleban-minutes-parse", ("time", args[3]), ("help", Help)));
                    return;
                }

                break;
            case 5:
                target = args[0];
                job = args[1];
                reason = args[2];

                if (!uint.TryParse(args[3], out minutes))
                {
                    shell.WriteError(Loc.GetString("cmd-roleban-minutes-parse", ("time", args[3]), ("help", Help)));
                    return;
                }

                if (!Enum.TryParse(args[4], ignoreCase: true, out severity))
                {
                    shell.WriteLine(Loc.GetString("cmd-roleban-severity-parse", ("severity", args[4]), ("help", Help)));
                    return;
                }

                break;
            default:
                shell.WriteError(Loc.GetString("cmd-roleban-arg-count"));
                shell.WriteLine(Help);
                return;
        }

        
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

        var located = await _locator.LookupIdByNameOrIdAsync(target);
        if (located == null)
        {
            shell.WriteError(Loc.GetString("cmd-roleban-name-parse"));
            return;
        }

        var targetUid = located.UserId;
        var targetHWid = located.LastHWId;

        _bans.CreateRoleBan(targetUid, located.Username, shell.Player?.UserId, null, targetHWid, job, minutes, severity, reason, DateTimeOffset.UtcNow);
    
        DateTimeOffset? expires = null;
        if (minutes > 0)
        {
            expires = DateTimeOffset.Now + TimeSpan.FromMinutes(minutes);
        }

        var roleBanId = await dbMan.GetLastServerRoleBanId();

        if (!string.IsNullOrEmpty(webhookUrl))
        {
            var payload = new WebhookPayload
            {
                Username = "Это роль-бан",
                AvatarUrl = "",
                Embeds = new List<WebhookEmbed>
                {
                    new WebhookEmbed
                    {
                        Color = 0xffa500,
                        Description = GenerateBanDescription(roleBanId, target, shell.Player, minutes, reason, expires, job),
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
    

    public CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        var durOpts = new CompletionOption[]
        {
            new("0", Loc.GetString("cmd-roleban-hint-duration-1")),
            new("1440", Loc.GetString("cmd-roleban-hint-duration-2")),
            new("4320", Loc.GetString("cmd-roleban-hint-duration-3")),
            new("10080", Loc.GetString("cmd-roleban-hint-duration-4")),
            new("20160", Loc.GetString("cmd-roleban-hint-duration-5")),
            new("43800", Loc.GetString("cmd-roleban-hint-duration-6")),
        };

        var severities = new CompletionOption[]
        {
            new("none", Loc.GetString("admin-note-editor-severity-none")),
            new("minor", Loc.GetString("admin-note-editor-severity-low")),
            new("medium", Loc.GetString("admin-note-editor-severity-medium")),
            new("high", Loc.GetString("admin-note-editor-severity-high")),
        };

        return args.Length switch
        {
            1 => CompletionResult.FromHintOptions(CompletionHelper.SessionNames(),
                Loc.GetString("cmd-roleban-hint-1")),
            2 => CompletionResult.FromHintOptions(CompletionHelper.PrototypeIDs<JobPrototype>(),
                Loc.GetString("cmd-roleban-hint-2")),
            3 => CompletionResult.FromHint(Loc.GetString("cmd-roleban-hint-3")),
            4 => CompletionResult.FromHintOptions(durOpts, Loc.GetString("cmd-roleban-hint-4")),
            5 => CompletionResult.FromHintOptions(severities, Loc.GetString("cmd-roleban-hint-5")),
            _ => CompletionResult.Empty
        };
    }

    private string GenerateBanDescription(int banId, string target, ICommonSession? session, uint minutes, string reason, DateTimeOffset? expires, string job)
    {
        var builder = new StringBuilder();

        builder.AppendLine($"### **Роль-бан | ID {banId}**");
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

        builder.AppendLine($"**Роль:** {job}");

        if (expires != null)
        {
            builder.AppendLine($"**Дата снятия наказания:** {expires}");
        }

        builder.Append($"**Наказание выдал(-а):** ");

        if (session != null)
        {
            builder.AppendLine($"*{session.Name}*");
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
