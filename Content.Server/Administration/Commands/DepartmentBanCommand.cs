using System.Net.Http;
using System.Text;
using System.Text.Json;
using Content.Server.Administration.Managers;
using Content.Server.Database;
using Content.Server.Discord;
using Content.Server.Discord.Webhooks;
using Content.Server.GameTicking;
using Content.Shared.Administration;
using Content.Shared.CCVar;
using Content.Shared.Database;
using Content.Shared.Roles;
using Robust.Shared.Configuration;
using Robust.Shared.Console;
using Robust.Shared.Prototypes;
using Robust.Shared.Player;

namespace Content.Server.Administration.Commands;

[AdminCommand(AdminFlags.Ban)]
public sealed class DepartmentBanCommand : IConsoleCommand
{
    [Dependency] private readonly IPrototypeManager _protoManager = default!;
    [Dependency] private readonly IPlayerLocator _locator = default!;
    [Dependency] private readonly IBanManager _banManager = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IServerDbManager _dbManager = default!;
    [Dependency] private readonly IEntitySystemManager _entitySystemManager = default!;

    private readonly HttpClient _httpClient = new();
    public string Command => "departmentban";
    public string Description => Loc.GetString("cmd-departmentban-desc");
    public string Help => Loc.GetString("cmd-departmentban-help");

    public async void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        string target;
        string department;
        string reason;
        uint minutes;
        var webhookUrl = _cfg.GetCVar(CCVars.DiscordBansWebhook);
        var serverName = _cfg.GetCVar(CCVars.GameHostName);

        if (!Enum.TryParse(_cfg.GetCVar(CCVars.DepartmentBanDefaultSeverity), out NoteSeverity severity))
        {
            Logger.WarningS("admin.department_ban",
                "Department ban severity could not be parsed from config! Defaulting to medium.");
            severity = NoteSeverity.Medium;
        }

        switch (args.Length)
        {
            case 3:
                target = args[0];
                department = args[1];
                reason = args[2];
                minutes = 0;
                break;
            case 4:
                target = args[0];
                department = args[1];
                reason = args[2];

                if (!uint.TryParse(args[3], out minutes))
                {
                    shell.WriteError(Loc.GetString("cmd-roleban-minutes-parse", ("time", args[3]), ("help", Help)));
                    return;
                }

                break;
            case 5:
                target = args[0];
                department = args[1];
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

        var startRoleBanId = await _dbManager.GetLastServerRoleBanId() + 1;

        DateTimeOffset? expires = null;
        if (minutes > 0)
        {
            expires = DateTimeOffset.Now + TimeSpan.FromMinutes(minutes);
        }

        var gameTicker = _entitySystemManager.GetEntitySystem<GameTicker>();
        var round = gameTicker.RunLevel switch
        {
            GameRunLevel.PreRoundLobby => gameTicker.RoundId == 0
                ? "pre-round lobby after server restart" // first round after server restart has ID == 0
                : $"pre-round lobby for round {gameTicker.RoundId + 1}",
            GameRunLevel.InRound => $"round {gameTicker.RoundId}",
            GameRunLevel.PostRound => $"post-round {gameTicker.RoundId}",
            _ => throw new ArgumentOutOfRangeException(nameof(gameTicker.RunLevel),
                $"{gameTicker.RunLevel} was not matched."),
        };

        if (!_protoManager.TryIndex<DepartmentPrototype>(department, out var departmentProto))
        {
            return;
        }

        var located = await _locator.LookupIdByNameOrIdAsync(target);
        if (located == null)
        {
            shell.WriteError(Loc.GetString("cmd-roleban-name-parse"));
            return;
        }

        var targetUid = located.UserId;
        var targetHWid = located.LastHWId;

        // If you are trying to remove the following variable, please don't. It's there because the note system groups role bans by time, reason and banning admin.
        // Without it the note list will get needlessly cluttered.
        var now = DateTimeOffset.UtcNow;
        foreach (var job in departmentProto.Roles)
        {
            _banManager.CreateRoleBan(targetUid, located.Username, shell.Player?.UserId, null, targetHWid, job, minutes,
                severity, reason, now);
        }

        if (!string.IsNullOrEmpty(webhookUrl))
        {
            var roleBanIdsString = "";

            if (departmentProto?.Roles != null && departmentProto.Roles.Count > 0)
            {
                int[] roleBanIds;
                roleBanIds = new int[departmentProto.Roles.Count];
                roleBanIds[0] = startRoleBanId;

                for (var i = 1; i < roleBanIds.Length; i++)
                {
                    roleBanIds[i] = roleBanIds[i - 1] + 1;
                }

                roleBanIdsString = string.Join(", ", roleBanIds);
            }


            var payload = new WebhookPayload
            {
                Username = "Это департмент-бан",
                AvatarUrl = "",
                Embeds = new List<WebhookEmbed>
                {
                    new WebhookEmbed
                    {
                        Color = 0xffea00,
                        Description = GenerateBanDescription(roleBanIdsString, target, shell.Player, minutes, reason,
                            expires, department),
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
            2 => CompletionResult.FromHintOptions(CompletionHelper.PrototypeIDs<DepartmentPrototype>(),
                Loc.GetString("cmd-roleban-hint-2")),
            3 => CompletionResult.FromHint(Loc.GetString("cmd-roleban-hint-3")),
            4 => CompletionResult.FromHintOptions(durOpts, Loc.GetString("cmd-roleban-hint-4")),
            5 => CompletionResult.FromHintOptions(severities, Loc.GetString("cmd-roleban-hint-5")),
            _ => CompletionResult.Empty
        };
    }

    private string GenerateBanDescription(string roleBanIdsString, string target, ICommonSession? session, uint minutes, string reason, DateTimeOffset? expires, string department)
    {
        var builder = new StringBuilder();

        builder.AppendLine($"### **Департмент-бан | IDs {roleBanIdsString}**");
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

        builder.AppendLine($"**Отдел:** {department}");

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
