using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Content.Server.Discord;
using Content.Shared.CCVar;
using Robust.Shared.Configuration;
using System.Threading.Tasks;

namespace Content.Server._Cats.Discord
{
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
            Console.WriteLine("Initializing BanWebhook");
            _config.OnValueChanged(CCVars.DiscordBanWebhook, OnWebhookChanged, true);
            _config.OnValueChanged(CCVars.DiscordBanFooterIcon, OnFooterIconChanged, true);
            _config.OnValueChanged(CCVars.DiscordBanAvatar, OnAvatarChanged, true);

            _sawmill = IoCManager.Resolve<ILogManager>().GetSawmill("BAN");
        }

        private void OnWebhookChanged(string url)
        {
            Console.WriteLine($"Webhook URL changed: {url}");
            _webhookUrl = url;

            if (string.IsNullOrEmpty(url))
            {
                _sawmill.Warning("Webhook URL is empty.");
                return;
            }

            var match = Regex.Match(url, @"^https://discord\.com/api/webhooks/(\d+)/((?!.*/).*)$");

            if (!match.Success)
            {
                _sawmill.Warning("Webhook URL does not appear to be valid. Using anyways...");
            }
        }

        public async Task GenerateWebhook(string admin, string user, string severity, uint? minutes, string reason)
        {
            Console.WriteLine("GenerateWebhook called");
            _sawmill.Info("GenerateWebhook called");
            try
            {
                if (string.IsNullOrEmpty(_webhookUrl))
                {
                    _sawmill.Error("Webhook URL is not set.");
                    return;
                }

                var payload = GenerateBanPayload(admin, user, severity, minutes, reason);
                var payloadJson = JsonSerializer.Serialize(payload);
                _sawmill.Info($"Payload JSON: {payloadJson}");
                Console.WriteLine($"Payload JSON: {payloadJson}");

                var request = await _httpClient.PostAsync($"{_webhookUrl}?wait=true",
                    new StringContent(payloadJson, Encoding.UTF8, "application/json"));

                var content = await request.Content.ReadAsStringAsync();
                _sawmill.Info($"Discord response: {content}");
                Console.WriteLine($"Discord response: {content}");

                if (request.IsSuccessStatusCode)
                {
                    _sawmill.Info("Webhook sent successfully.");
                    Console.WriteLine("Webhook sent successfully.");
                }
                else
                {
                    _sawmill.Log(LogLevel.Error, $"Discord returned bad status code when posting message: {request.StatusCode}\nResponse: {content}");
                    Console.WriteLine($"Discord returned bad status code when posting message: {request.StatusCode}\nResponse: {content}");
                }
            }
            catch (Exception ex)
            {
                _sawmill.Log(LogLevel.Error, $"Exception occurred while sending webhook: {ex.Message}\n{ex.StackTrace}");
                Console.WriteLine($"Exception occurred while sending webhook: {ex.Message}\n{ex.StackTrace}");
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

            _sawmill.Info($"Generated ban payload for user: {user}, admin: {admin}, reason: {reason}");
            Console.WriteLine($"Generated ban payload for user: {user}, admin: {admin}, reason: {reason}");

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
}