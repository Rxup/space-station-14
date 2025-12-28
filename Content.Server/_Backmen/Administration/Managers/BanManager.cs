using System.Collections.Immutable;
using System.Net;
using Content.Server.Database;
using Content.Server.GameTicking;
using Content.Server.Preferences.Managers;
using Content.Shared.Database;
using Content.Shared.Players.PlayTimeTracking;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Robust.Shared.Network;

// ReSharper disable once CheckNamespace
namespace Content.Server.Administration.Managers;

public sealed partial class BanManager
{
    public const string AntagPrefix = "Antag:";

    [Dependency] private readonly IServerPreferencesManager _prefs = default!;

    public async void CreateAntagban(NetUserId? target,
        string? targetUsername,
        NetUserId? banningAdmin,
        (IPAddress, int)? addressRange,
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

        role = string.Concat(AntagPrefix, role);
        DateTimeOffset? expires = null;
        if (minutes > 0)
        {
            expires = DateTimeOffset.Now + TimeSpan.FromMinutes(minutes.Value);
        }

        _systems.TryGetEntitySystem(out GameTicker? ticker);
        int? roundId = ticker == null || ticker.RoundId == 0 ? null : ticker.RoundId;
        var playtime = target == null
            ? TimeSpan.Zero
            : (await _db.GetPlayTimes(target.Value)).Find(p => p.Tracker == PlayTimeTrackingShared.TrackerOverall)
            ?.TimeSpent ?? TimeSpan.Zero;

        var banDef = new ServerRoleBanDef(
            null,
            target,
            addressRange,
            hwid,
            timeOfBan,
            expires,
            roundId,
            playtime,
            reason,
            severity,
            banningAdmin,
            null,
            role);

        if (!await AddRoleBan(banDef))
        {
            _chat.SendAdminAlert(Loc.GetString("cmd-antagban-existing",
                ("target", targetUsername ?? "null"),
                ("role", role)));
            return;
        }

        var length = expires == null
            ? Loc.GetString("cmd-antagban-inf")
            : Loc.GetString("cmd-antagban-until", ("expires", expires));
        _chat.SendAdminAlert(Loc.GetString("cmd-antagban-success",
            ("target", targetUsername ?? "null"),
            ("role", role),
            ("reason", reason),
            ("length", length)));

        var prefs = _prefs.GetPreferences(target!.Value);
        foreach (var (index, profile) in prefs.Characters)
        {
            var character = (HumanoidCharacterProfile)profile;
            character = character.WithAntagPreference(antag, false);
            await _prefs.SetProfile(target!.Value, index, character);
            if (_playerManager.TryGetSessionById(target.Value, out var session))
                _prefs.FinishLoad(session);
            if (session != null)
                SendRoleBans(session);
        }
    }
}
