using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Content.Corvax.Interfaces.Server;
using Content.Server.Backmen.Administration.Bwoink.Gpt.Models;
using Content.Shared.Corvax.TTS;
using Robust.Shared.Configuration;
using Robust.Shared.ContentPack;
using Robust.Shared.Utility;

namespace Content.Server.Backmen.VPN;

public sealed class VpnGuard : IServerVPNGuardManager
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IResourceManager _resourceManager = default!;

    private bool _isEnabled = false;
    private string _apiToken = "";

    private readonly HttpClient _httpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(5),
        BaseAddress = new Uri("https://www.ipqualityscore.com/api/json/ip/")
    };

    private ResPath GetCacheId(IPAddress ip)
    {
        var pathIp = new List<string>();
        var bytes = ip.GetAddressBytes();
        for (var i = 0; i < bytes.Length / 2; i++)
        {
            pathIp.Add(BitConverter.ToString(bytes, i, 2));
        }

        var resPath = new ResPath($"vpnguard/{ip.AddressFamily}/{string.Join('/', pathIp)}/{ip}.json").ToRootedPath();
        _resourceManager.UserData.CreateDir(resPath.Directory);
        return resPath.ToRootedPath();
    }

    private ISawmill _sawmill = default!;

    public void Initialize()
    {
        _sawmill = Logger.GetSawmill("vpnguard");
        _cfg.OnValueChanged(Shared.Backmen.CCVar.CCVars.VpnGuardEnabled, OnEnableCVarChanged, true);
        _cfg.OnValueChanged(Shared.Backmen.CCVar.CCVars.VpnGuardToken, OnTokenCVarChanged, true);
    }

    private void OnTokenCVarChanged(string obj)
    {
        _sawmill.Info("Change VPNGuard token");
        _apiToken = obj;
    }

    private void OnEnableCVarChanged(bool toggle)
    {
        _sawmill.Info("VPNGuard {0}", toggle);
        _isEnabled = toggle;
    }

    public async Task<bool> IsConnectionVpn(IPAddress ip)
    {
        if (!_isEnabled || string.IsNullOrEmpty(_apiToken))
        {
            return false; // disabled
        }

        try
        {
            GeoData? response;
            var cache = GetCacheId(ip);
            if (_resourceManager.UserData.Exists(cache))
            {
                await using var reader = _resourceManager.UserData.OpenRead(cache);
                response = JsonSerializer.Deserialize<GeoData>(reader);
            }
            else
            {
                using var resp = await _httpClient.GetAsync(
                    $"{_apiToken}/{ip.ToString()}?strictness=0&allow_public_access_points=true&fast=true&lighter_penalties=true&mobile=false");
                resp.EnsureSuccessStatusCode();
                var content = await resp.Content.ReadAsStringAsync();
                response = JsonSerializer.Deserialize<GeoData>(content);
                if (response is { Success: false })
                {
                    _sawmill.Error($"Ошибка проверки адреса: {content}");
                    return false;
                }

                if (response != null)
                {
                    await using var writer = _resourceManager.UserData.OpenWrite(cache);
                    await writer.WriteAsync(Encoding.UTF8.GetBytes(content));
                }
            }

            if (response == null)
            {
                _sawmill.Error($"Ошибка проверки адреса: {ip}");
                return false;
            }

            return
                (response.Proxy ?? false) ||
                (response.VPN ?? false);
        }
        catch (Exception err)
        {
            _sawmill.Error("error IsConnectionVpn: {0}", err.ToString());
            return false;
        }
    }
}

#region Models

public record GeoData
{
    [JsonPropertyName("message")]
    public string? Message { get; init; }

    [JsonPropertyName("success")]
    public bool? Success { get; init; }

    [JsonPropertyName("proxy")]
    public bool? Proxy { get; init; }

    [JsonPropertyName("ISP")]
    public string? ISP { get; init; }

    [JsonPropertyName("organization")]
    public string? Organization { get; init; }

    [JsonPropertyName("ASN")]
    public int? ASN { get; init; }

    [JsonPropertyName("host")]
    public string? Host { get; init; }

    [JsonPropertyName("country_code")]
    public string? CountryCode { get; init; }

    [JsonPropertyName("city")]
    public string? City { get; init; }

    [JsonPropertyName("region")]
    public string? Region { get; init; }

    [JsonPropertyName("is_crawler")]
    public bool? IsCrawler { get; init; }

    [JsonPropertyName("connection_type")]
    public string? ConnectionType { get; init; }

    [JsonPropertyName("latitude")]
    public double? Latitude { get; init; }

    [JsonPropertyName("longitude")]
    public double? Longitude { get; init; }

    [JsonPropertyName("zip_code")]
    public string? ZipCode { get; init; }

    [JsonPropertyName("timezone")]
    public string? Timezone { get; init; }

    [JsonPropertyName("vpn")]
    public bool? VPN { get; init; }

    [JsonPropertyName("tor")]
    public bool? TOR { get; init; }

    [JsonPropertyName("active_vpn")]
    public bool? ActiveVPN { get; init; }

    [JsonPropertyName("active_tor")]
    public bool? ActiveTOR { get; init; }

    [JsonPropertyName("recent_abuse")]
    public bool? RecentAbuse { get; init; }

    [JsonPropertyName("frequent_abuser")]
    public bool? FrequentAbuser { get; init; }

    [JsonPropertyName("high_risk_attacks")]
    public bool? HighRiskAttacks { get; init; }

    [JsonPropertyName("abuse_velocity")]
    public string? AbuseVelocity { get; init; }

    [JsonPropertyName("bot_status")]
    public bool? BotStatus { get; init; }

    [JsonPropertyName("shared_connection")]
    public bool? SharedConnection { get; init; }

    [JsonPropertyName("dynamic_connection")]
    public bool? DynamicConnection { get; init; }

    [JsonPropertyName("security_scanner")]
    public bool? SecurityScanner { get; init; }

    [JsonPropertyName("trusted_network")]
    public bool? TrustedNetwork { get; init; }

    [JsonPropertyName("mobile")]
    public bool? Mobile { get; init; }

    [JsonPropertyName("fraud_score")]
    public int? FraudScore { get; init; }

    [JsonPropertyName("operating_system")]
    public string? OperatingSystem { get; init; }

    [JsonPropertyName("browser")]
    public string? Browser { get; init; }

    [JsonPropertyName("device_model")]
    public string? DeviceModel { get; init; }

    [JsonPropertyName("device_brand")]
    public string? DeviceBrand { get; init; }

    [JsonPropertyName("request_id")]
    public string? RequestId { get; init; }
}

#endregion
