using Content.Server.Preferences.Managers;
using Content.Shared.Database;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Robust.Shared.Network;

// ReSharper disable once CheckNamespace
namespace Content.Server.Administration.Managers;

public sealed partial class BanManager
{
    public const string AntagPrefix = "Antag:";

    [Dependency] private IServerPreferencesManager _prefs = default!;

    public async void CreateAntagban(NetUserId? target,
        string? targetUsername,
        NetUserId? banningAdmin,
        (System.Net.IPAddress, int)? addressRange,
        ImmutableTypedHwid? hwid,
        string role,
        uint? minutes,
        NoteSeverity severity,
        string reason,
        DateTimeOffset timeOfBan)
    {
        if (!_prototypeManager.TryIndex(role, out AntagPrototype? antag))
        {
            throw new ArgumentException($"Invalid antag '{role}'", nameof(role));
        }

        var banInfo = new CreateRoleBanInfo(reason);
        banInfo.AddAntag(antag.ID);
        banInfo.WithSeverity(severity);
        banInfo.WithBanningAdmin(banningAdmin);

        if (target != null)
            banInfo.AddUser(target.Value, targetUsername ?? target.Value.ToString());

        if (addressRange != null)
            banInfo.AddAddressRange(addressRange.Value);

        banInfo.AddHWId(hwid);

        if (minutes > 0)
            banInfo.WithMinutes(minutes.Value);

        CreateRoleBan(banInfo);

        if (target == null)
            return;

        var prefs = _prefs.GetPreferences(target.Value);
        foreach (var (index, profile) in prefs.Characters)
        {
            var character = (HumanoidCharacterProfile)profile;
            character = character.WithAntagPreference(antag, false);
            await _prefs.SetProfile(target.Value, index, character);
            if (_playerManager.TryGetSessionById(target.Value, out var session))
                _prefs.FinishLoad(session);
            if (session != null)
                SendRoleBans(session);
        }
    }
}
