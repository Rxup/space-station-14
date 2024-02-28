using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Content.Corvax.Interfaces.Server;
using Content.Server.Backmen.Administration.Bwoink.Gpt.Models;
using Robust.Shared.Configuration;

namespace Content.Server.Backmen.VPN;

public sealed class VpnGuard : IServerVPNGuardManager
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;

    private bool _isEnabled = false;
    private string _apiToken = "";

    private readonly HttpClient _httpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(5),
        BaseAddress = new Uri("https://www.ipqualityscore.com/api/json/ip/")
    };

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
            var resp = await _httpClient.GetAsync($"{_apiToken}/{ip.ToString()}?strictness=0&allow_public_access_points=true&fast=true&lighter_penalties=true&mobile=false");

            var info = JsonSerializer.Deserialize<TransactionResponse>(await resp.Content.ReadAsStringAsync());
        }
        catch (Exception err)
        {
            _sawmill.Error("error IsConnectionVpn: {0}",err.ToString());
            return false;
        }
    }
}

#region Models
public record TransactionDetails(
    bool ValidBillingAddress,
    bool ValidShippingAddress,
    bool ValidBillingEmail,
    bool ValidShippingEmail,
    bool RiskyBillingPhone,
    bool RiskyShippingPhone,
    string BillingPhoneCarrier,
    string ShippingPhoneCarrier,
    string BillingPhoneLineType,
    string ShippingPhoneLineType,
    string BillingPhoneCountry,
    string BillingPhoneCountryCode,
    string ShippingPhoneCountry,
    string ShippingPhoneCountryCode,
    bool FraudulentBehavior,
    string BinCountry,
    string BinType,
    string BinBankName,
    int RiskScore,
    string[] RiskFactors,
    bool IsPrepaidCard,
    bool RiskyUsername,
    bool ValidBillingPhone,
    bool ValidShippingPhone,
    bool LeakedBillingEmail,
    bool LeakedShippingEmail,
    bool LeakedUserData,
    string UserActivity,
    string PhoneNameIdentityMatch,
    string PhoneEmailIdentityMatch,
    string PhoneAddressIdentityMatch,
    string EmailNameIdentityMatch,
    string NameAddressIdentityMatch,
    string AddressEmailIdentityMatch
);

public record Location(
    double Latitude,
    double Longitude,
    string ZipCode,
    string Timezone,
    bool Vpn,
    bool Tor,
    bool ActiveVpn,
    bool ActiveTor,
    bool RecentAbuse,
    bool FrequentAbuser,
    bool HighRiskAttacks,
    string AbuseVelocity,
    bool BotStatus,
    bool SharedConnection,
    bool DynamicConnection,
    bool SecurityScanner,
    bool TrustedNetwork,
    bool Mobile,
    int FraudScore,
    string OperatingSystem,
    string Browser,
    string DeviceModel,
    string DeviceBrand
);

public record TransactionResponse(
    string Message,
    bool Success,
    bool Proxy,
    string ISP,
    string Organization,
    int ASN,
    string Host,
    string CountryCode,
    string City,
    string Region,
    bool IsCrawler,
    string ConnectionType,
    Location Location,
    TransactionDetails TransactionDetails,
    string RequestId
);

#endregion
