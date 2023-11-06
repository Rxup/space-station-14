using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Content.Corvax.Interfaces.Server;
using Content.Server.Database;
using Content.Server.Humanoid;
using Content.Shared.CCVar;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Robust.Shared.Configuration;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;


namespace Content.Server.Preferences.Managers
{
    /// <summary>
    /// Sends <see cref="MsgPreferencesAndSettings"/> before the client joins the lobby.
    /// Receives <see cref="MsgSelectCharacter"/> and <see cref="MsgUpdateCharacter"/> at any time.
    /// </summary>
    public sealed class ServerPreferencesManager : IServerPreferencesManager
    {
        [Dependency] private readonly IServerNetManager _netManager = default!;
        [Dependency] private readonly IConfigurationManager _cfg = default!;
        [Dependency] private readonly IServerDbManager _db = default!;
        [Dependency] private readonly IPrototypeManager _protos = default!;
        private IServerSponsorsManager? _sponsors;

        // Cache player prefs on the server so we don't need as much async hell related to them.
        private readonly Dictionary<NetUserId, PlayerPrefData> _cachedPlayerPrefs =
            new();

        // private int MaxCharacterSlots => _cfg.GetCVar(CCVars.GameMaxCharacterSlots); // Corvax-Sponsors

        public void Init()
        {
            IoCManager.Instance!.TryResolveType(out _sponsors); // Corvax-Sponsors
            _netManager.RegisterNetMessage<MsgPreferencesAndSettings>();
            _netManager.RegisterNetMessage<MsgSelectCharacter>(HandleSelectCharacterMessage);
            _netManager.RegisterNetMessage<MsgUpdateCharacter>(HandleUpdateCharacterMessage);
            _netManager.RegisterNetMessage<MsgDeleteCharacter>(HandleDeleteCharacterMessage);
        }

        private async void HandleSelectCharacterMessage(MsgSelectCharacter message)
        {
            var index = message.SelectedCharacterIndex;
            var userId = message.MsgChannel.UserId;

            if (!_cachedPlayerPrefs.TryGetValue(userId, out var prefsData) || !prefsData.PrefsLoaded)
            {
                Logger.WarningS("prefs", $"User {userId} tried to modify preferences before they loaded.");
                return;
            }

            if (index < 0 || index >= GetMaxUserCharacterSlots(userId)) // Corvax-Sponsors
            {
                return;
            }

            var curPrefs = prefsData.Prefs!;

            if (!curPrefs.Characters.ContainsKey(index))
            {
                // Non-existent slot.
                return;
            }

            prefsData.Prefs = new PlayerPreferences(curPrefs.Characters, index, curPrefs.AdminOOCColor);

            if (ShouldStorePrefs(message.MsgChannel.AuthType))
            {
                await _db.SaveSelectedCharacterIndexAsync(message.MsgChannel.UserId, message.SelectedCharacterIndex);
            }
        }

        private async void HandleUpdateCharacterMessage(MsgUpdateCharacter message)
        {
            var slot = message.Slot;
            var profile = message.Profile;
            var userId = message.MsgChannel.UserId;

            if (profile == null)
            {
                Logger.WarningS("prefs",
                    $"User {userId} sent a {nameof(MsgUpdateCharacter)} with a null profile in slot {slot}.");
                return;
            }

            if (!_cachedPlayerPrefs.TryGetValue(userId, out var prefsData) || !prefsData.PrefsLoaded)
            {
                Logger.WarningS("prefs", $"User {userId} tried to modify preferences before they loaded.");
                return;
            }

            if (slot < 0 || slot >= GetMaxUserCharacterSlots(userId)) // Corvax-Sponsors
            {
                return;
            }

            var curPrefs = prefsData.Prefs!;

            // Corvax-Sponsors-Start: Ensure removing sponsor markings if client somehow bypassed client filtering
            // WARN! It's not removing markings from DB!
            var sponsorPrototypes = _sponsors != null && _sponsors.TryGetPrototypes(message.MsgChannel.UserId, out var prototypes)
                ? prototypes.ToArray()
                : new string[]{};
            profile.EnsureValid(sponsorPrototypes);
            // Corvax-Sponsors-End
            var profiles = new Dictionary<int, ICharacterProfile>(curPrefs.Characters)
            {
                [slot] = profile
            };

            prefsData.Prefs = new PlayerPreferences(profiles, slot, curPrefs.AdminOOCColor);

            if (ShouldStorePrefs(message.MsgChannel.AuthType))
            {
                await _db.SaveCharacterSlotAsync(message.MsgChannel.UserId, message.Profile, message.Slot);
            }
        }

        private async void HandleDeleteCharacterMessage(MsgDeleteCharacter message)
        {
            var slot = message.Slot;
            var userId = message.MsgChannel.UserId;

            if (!_cachedPlayerPrefs.TryGetValue(userId, out var prefsData) || !prefsData.PrefsLoaded)
            {
                Logger.WarningS("prefs", $"User {userId} tried to modify preferences before they loaded.");
                return;
            }

            if (slot < 0 || slot >= GetMaxUserCharacterSlots(userId)) // Corvax-Sponsors
            {
                return;
            }

            var curPrefs = prefsData.Prefs!;

            // If they try to delete the slot they have selected then we switch to another one.
            // Of course, that's only if they HAVE another slot.
            int? nextSlot = null;
            if (curPrefs.SelectedCharacterIndex == slot)
            {
                // That ! on the end is because Rider doesn't like .NET 5.
                var (ns, profile) = curPrefs.Characters.FirstOrDefault(p => p.Key != message.Slot)!;
                if (profile == null)
                {
                    // Only slot left, can't delete.
                    return;
                }

                nextSlot = ns;
            }

            var arr = new Dictionary<int, ICharacterProfile>(curPrefs.Characters);
            arr.Remove(slot);

            prefsData.Prefs = new PlayerPreferences(arr, nextSlot ?? curPrefs.SelectedCharacterIndex, curPrefs.AdminOOCColor);

            if (ShouldStorePrefs(message.MsgChannel.AuthType))
            {
                if (nextSlot != null)
                {
                    await _db.DeleteSlotAndSetSelectedIndex(userId, slot, nextSlot.Value);
                }
                else
                {
                    await _db.SaveCharacterSlotAsync(userId, null, slot);
                }
            }
        }

        // Should only be called via UserDbDataManager.
        public async Task LoadData(ICommonSession session, CancellationToken cancel)
        {
            if (!ShouldStorePrefs(session.ConnectedClient.AuthType))
            {
                // Don't store data for guests.
                var prefsData = new PlayerPrefData
                {
                    PrefsLoaded = true,
                    Prefs = new PlayerPreferences(
                        new[] {new KeyValuePair<int, ICharacterProfile>(0, HumanoidCharacterProfile.Random())},
                        0, Color.Transparent)
                };

                _cachedPlayerPrefs[session.UserId] = prefsData;
            }
            else
            {
                var prefsData = new PlayerPrefData();
                var loadTask = LoadPrefs();
                _cachedPlayerPrefs[session.UserId] = prefsData;

                await loadTask;

                async Task LoadPrefs()
                {
                    var prefs = await GetOrCreatePreferencesAsync(session.UserId);
                    // Corvax-Sponsors-Start: Remove sponsor markings from expired sponsors
                    foreach (var (_, profile) in prefs.Characters)
                    {
                        var sponsorPrototypes = _sponsors != null && _sponsors.TryGetPrototypes(session.UserId, out var prototypes)
                            ? prototypes.ToArray()
                            : new string[]{};
                        profile.EnsureValid(sponsorPrototypes);
                    }
                    // Corvax-Sponsors-End
                    prefsData.Prefs = prefs;
                    prefsData.PrefsLoaded = true;

                    var msg = new MsgPreferencesAndSettings();
                    msg.Preferences = prefs;
                    msg.Settings = new GameSettings
                    {
                        MaxCharacterSlots = GetMaxUserCharacterSlots(session.UserId),  // Corvax-Sponsors
                    };
                    _netManager.ServerSendMessage(msg, session.ConnectedClient);
                }
            }
        }

        public void OnClientDisconnected(ICommonSession session)
        {
            _cachedPlayerPrefs.Remove(session.UserId);
        }

        public bool HavePreferencesLoaded(ICommonSession session)
        {
            return _cachedPlayerPrefs.ContainsKey(session.UserId);
        }

        // Corvax-Sponsors-Start: Calculate total available users slots with sponsors
        private int GetMaxUserCharacterSlots(NetUserId userId)
        {
            var maxSlots = _cfg.GetCVar(CCVars.GameMaxCharacterSlots);
            var extraSlots = _sponsors?.GetExtraCharSlots(userId) ?? 0;
            return maxSlots + extraSlots;
        }
        // Corvax-Sponsors-End

        /// <summary>
        /// Tries to get the preferences from the cache
        /// </summary>
        /// <param name="userId">User Id to get preferences for</param>
        /// <param name="playerPreferences">The user preferences if true, otherwise null</param>
        /// <returns>If preferences are not null</returns>
        public bool TryGetCachedPreferences(NetUserId userId,
            [NotNullWhen(true)] out PlayerPreferences? playerPreferences)
        {
            if (_cachedPlayerPrefs.TryGetValue(userId, out var prefs))
            {
                playerPreferences = prefs.Prefs;
                return prefs.Prefs != null;
            }

            playerPreferences = null;
            return false;
        }

        /// <summary>
        /// Retrieves preferences for the given username from storage.
        /// Creates and saves default preferences if they are not found, then returns them.
        /// </summary>
        public PlayerPreferences GetPreferences(NetUserId userId)
        {
            var prefs = _cachedPlayerPrefs[userId].Prefs;
            if (prefs == null)
            {
                throw new InvalidOperationException("Preferences for this player have not loaded yet.");
            }

            return prefs;
        }

        private async Task<PlayerPreferences> GetOrCreatePreferencesAsync(NetUserId userId)
        {
            var prefs = await _db.GetPlayerPreferencesAsync(userId);
            if (prefs is null)
            {
                return await _db.InitPrefsAsync(userId, HumanoidCharacterProfile.Random());
            }

            return SanitizePreferences(prefs);
        }

        private PlayerPreferences SanitizePreferences(PlayerPreferences prefs)
        {
            // Clean up preferences in case of changes to the game,
            // such as removed jobs still being selected.

            return new PlayerPreferences(prefs.Characters.Select(p =>
            {
                ICharacterProfile newProf;
                switch (p.Value)
                {
                    case HumanoidCharacterProfile hp:
                    {
                        var prototypeManager = IoCManager.Resolve<IPrototypeManager>();
                        var selectedSpecies = HumanoidAppearanceSystem.DefaultSpecies;

                        if (prototypeManager.TryIndex<SpeciesPrototype>(hp.Species, out var species) && species.RoundStart)
                        {
                            selectedSpecies = hp.Species;
                        }

                        newProf = hp
                            .WithJobPriorities(
                                hp.JobPriorities.Where(job =>
                                    _protos.HasIndex<JobPrototype>(job.Key)))
                            .WithAntagPreferences(
                                hp.AntagPreferences.Where(antag =>
                                    _protos.HasIndex<AntagPrototype>(antag)))
                            .WithSpecies(selectedSpecies);
                        break;
                    }
                    default:
                        throw new NotSupportedException();
                }

                return new KeyValuePair<int, ICharacterProfile>(p.Key, newProf);
            }), prefs.SelectedCharacterIndex, prefs.AdminOOCColor);
        }

        public IEnumerable<KeyValuePair<NetUserId, ICharacterProfile>> GetSelectedProfilesForPlayers(
            List<NetUserId> usernames)
        {
            return usernames
                .Select(p => (_cachedPlayerPrefs[p].Prefs, p))
                .Where(p => p.Prefs != null)
                .Select(p =>
                {
                    var idx = p.Prefs!.SelectedCharacterIndex;
                    return new KeyValuePair<NetUserId, ICharacterProfile>(p.p, p.Prefs!.GetProfile(idx));
                });
        }

        internal static bool ShouldStorePrefs(LoginType loginType)
        {
            return loginType.HasStaticUserId();
        }

        private sealed class PlayerPrefData
        {
            public bool PrefsLoaded;
            public PlayerPreferences? Prefs;
        }
    }
}
