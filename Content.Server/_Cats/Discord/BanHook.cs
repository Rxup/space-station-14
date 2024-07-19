using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks
using System.Text.RegularExpressions;
using Content.Server.Discord;
using Content.Shared.CCVar;
using Robust.Shared.Configuration;

namespace Content.Server._Cats.Discord;
public sealed class BanWebhook
{
    [Dependency] private readonly IConfigurationManager _config = default!;

    private ISawmill _sawmill = default!;
    private readonly HttpClient _httpClient = new();
    private string _webhookUrl = string.Empty;
    private string _footerIconUrl = string.Empty;
    private string _avatarUrl = string.Empty;

    public void Initialize()
    {
        _config.OnValueChanged(CCVars.DiscordBanWebhook, OnWebhookChanged, true);
        _config.OnValueChanged(CCVars.DiscordBanFooterIcon, OnFooterIconChanged, true);
        _config.OnValueChanged(CCVars.DiscordBanAvatar, OnAvatarChanged, true);

        _sawmill = IoCManager.Resolve<ILogManager>().GetSawmill("BAN");
    }

    private void OnWebhookChanged(string url)
    {
        _webhookUrl = url;

        if (string.IsNullOrEmpty(url))
            return;

        var match = Regex.Match(url, @"^https://discord\.com/api/webhooks/(\d+)/((?!.*/).*)$");

        if (!match.Success)
        {
            _sawmill.Warning("Webhook URL does not appear to be valid. Using anyways...");
        }
    }

    public async Task GenerateWebhook(string admin, string user, string severity, uint? minutes, string reason)
    {
        if (string.IsNullOrEmpty(_webhookUrl))
        {
            _sawmill.Error("Webhook URL is not set.");
            return;
        }

        var payload = GenerateBanPayload(admin, user, severity, minutes, reason);
        _sawmill.Info($"Sending webhook to {_webhookUrl}");

        var request = await _httpClient.PostAsync($"{_webhookUrl}?wait=true",
            new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"));

        var content = await request.Content.ReadAsStringAsync();

        if (request.IsSuccessStatusCode)
        {
            _sawmill.Info("Webhook sent successfully.");
        }
        else
        {
            _sawmill.Log(LogLevel.Error, $"Discord returned bad status code when posting message: {request.StatusCode}\nResponse: {content}");
        }
    }

    private WebhookPayload GenerateBanPayload(string admin, string user, string severity, uint? minutes, string reason)
    {
        var banType = Loc.GetString("ban-embed-perm");
        var timeNow = DateTime.Now.ToUniversalTime();
        var color = 0x6A00;
        var expires = string.Empty;

        if (minutes.HasValue && minutes != 0)
        {
            var expirationDate = DateTime.UtcNow.Add(TimeSpan.FromMinutes(minutes.Value));
            banType = Loc.GetString("ban-embed-temp", ("time", minutes));
            expires = $"**Истекает:** {expirationDate}\n";
            color = 0x513EA5;
        }

        return new WebhookPayload
        {
            AvatarUrl = string.IsNullOrWhiteSpace(_avatarUrl) ? null : _avatarUrl,
            Embeds = new List<WebhookEmbed>
            {
                new WebhookEmbed
                {
                    Color = color,
                    Title = banType,
                    Footer = new WebhookEmbedFooter
                    {
                        Text = Loc.GetString("ban-embed-footer", ("severity", severity)),
                        IconUrl = string.IsNullOrWhiteSpace(_footerIconUrl) ? null : _footerIconUrl
                    },
                    Description =
                        $"**Нарушитель**: {user.Split(" ")[0]}\n" +
                        $"**Администратор:** {admin}\n" +
                        $"\n" +
                        $"**Выдан:** {timeNow}\n" +
                        expires +
                        $"\n" +
                        $"**Причина:** {reason}"
                }
            },
        };
    }

    private void OnFooterIconChanged(string url)
    {
        _footerIconUrl = url;
    }

    private void OnAvatarChanged(string url)
    {
        _avatarUrl = url;
    }
}
