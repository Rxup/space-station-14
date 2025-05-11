using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Content.Server.Discord;
using Content.Shared.CCVar;
using Content.DeadSpace.Interfaces.Server;
using Robust.Shared.Configuration;

namespace Content.Server._Erida;
public sealed class DiscordBansSystem : EntitySystem

{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    private readonly HttpClient _httpClient = new();

    public  async Task SendBan(string? target,
        string? admin,
        uint? minutes,
        string reason,
        DateTimeOffset? expires,
        string? job,
        int color,
        string ban_type,
        int? roundId)
    {

        var webhookUrl = _cfg.GetCVar(CCVars.DiscordBansWebhook);
        var serverName = _cfg.GetCVar(CCVars.GameHostName);

        serverName = serverName[..Math.Min(serverName.Length, 1500)];
        if (string.IsNullOrEmpty(webhookUrl))
            return;

        var payload = new WebhookPayload
        {
            Username = serverName,
            AvatarUrl = "",
            Embeds = new List<WebhookEmbed>
            {
                new WebhookEmbed
                {
                    Color = color,
                    Description = GenerateBanDescription(target,
                        admin,
                        Convert.ToUInt32(minutes),
                        reason,
                        expires,
                        job,
                        color,
                        ban_type),
                    Footer = new WebhookEmbedFooter
                    {
                        Text = $"(раунд {roundId})"
                    }
                }
            }
        };

        await _httpClient.PostAsync($"{webhookUrl}?wait=true",
            new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"));
    }

    private string GenerateBanDescription(string? target,
        string? admin,
        uint minutes,
        string reason,
        DateTimeOffset? expires,
        string? job,
        int color,
        string ban_type)
    {
        var builder = new StringBuilder();

        builder.AppendLine($"### **{ban_type}**");
        builder.AppendLine($"**Нарушитель:** *{target}*");
        builder.AppendLine($"**Причина:** {reason}");

        builder.Append($"**Длительность:** ");

        if (expires != null)
        {
            var banDuration = TimeSpan.FromMinutes(minutes);
            builder.Append($"{banDuration.Days} {NumWord(banDuration.Days, "день", "дня", "дней")}, ");
            builder.Append($"{banDuration.Hours} {NumWord(banDuration.Hours, "час", "часа", "часов")}, ");
            builder.AppendLine($"{banDuration.Minutes} {NumWord(banDuration.Minutes, "минута", "минуты", "минут")}");

        }
        else
        {
            builder.AppendLine($"***Навсегда***");
        }

        if (job != null)
        {
            builder.AppendLine($"**Роль:** {job}");
        }

        if (expires != null)
        {
            builder.AppendLine($"**Дата снятия наказания:** {expires}");
        }

        builder.Append($"**Наказание выдал(-а):** ");

        if (admin != null)
        {
            builder.AppendLine($"*{admin}*");
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
