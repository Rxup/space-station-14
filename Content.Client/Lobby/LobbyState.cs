using System.Linq;
using System.Numerics;
using Content.Client.Backmen.UI.Buttons;
using Content.Client.Audio;
using Content.Client.Changelog;
using Content.Client.GameTicking.Managers;
using Content.Client.LateJoin;
using Content.Client.Lobby.UI;
using Content.Client.Message;
using Content.Client.Resources;
using Content.Client.UserInterface.Systems.Chat;
using Content.Client.Voting;
using Content.Shared.CCVar;
using Robust.Client;
using Robust.Client.Console;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Configuration;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client.Lobby
{
    public sealed class LobbyState : Robust.Client.State.State
    {
        [Dependency] private readonly IBaseClient _baseClient = default!;
        [Dependency] private readonly IConfigurationManager _cfg = default!;
        [Dependency] private readonly IClientConsoleHost _consoleHost = default!;
        [Dependency] private readonly IEntityManager _entityManager = default!;
        [Dependency] private readonly IResourceCache _resourceCache = default!;
        [Dependency] private readonly IUserInterfaceManager _userInterfaceManager = default!;
        [Dependency] private readonly IGameTiming _gameTiming = default!;
        [Dependency] private readonly IVoteManager _voteManager = default!;
        [Dependency] private readonly ChangelogManager _changelog = default!; // BACKMEN EDIT
        private ClientGameTicker _gameTicker = default!;
        private ContentAudioSystem _contentAudioSystem = default!;

        protected override Type? LinkedScreenType { get; } = typeof(LobbyGui);
        public LobbyGui? Lobby;

        protected override void Startup()
        {
            if (_userInterfaceManager.ActiveScreen == null)
            {
                return;
            }

            Lobby = (LobbyGui) _userInterfaceManager.ActiveScreen;

            var chatController = _userInterfaceManager.GetUIController<ChatUIController>();
            _gameTicker = _entityManager.System<ClientGameTicker>();
            _contentAudioSystem = _entityManager.System<ContentAudioSystem>();
            _contentAudioSystem.LobbySoundtrackChanged += UpdateLobbySoundtrackInfo;

            chatController.SetMainChat(true);

            _voteManager.SetPopupContainer(Lobby.VoteContainer);
            LayoutContainer.SetAnchorPreset(Lobby, LayoutContainer.LayoutPreset.Wide);

            var lobbyNameCvar = _cfg.GetCVar(CCVars.ServerLobbyName);
            var serverName = _baseClient.GameInfo?.ServerName ?? string.Empty;

            Lobby.ServerName.Text = string.IsNullOrEmpty(lobbyNameCvar)
                ? Loc.GetString("ui-lobby-title", ("serverName", serverName))
                : lobbyNameCvar;

            var width = _cfg.GetCVar(CCVars.ServerLobbyRightPanelWidth);
            Lobby.RightSide.SetWidth = width;

            UpdateLobbyUi();

            Lobby.CharacterSetupButton.OnPressed += OnSetupPressed; // BACKMEN EDIT
            Lobby.ReadyButton.OnPressed += OnReadyPressed;
            Lobby.ReadyButton.OnToggled += OnReadyToggled;

            _gameTicker.InfoBlobUpdated += UpdateLobbyUi;
            _gameTicker.LobbyStatusUpdated += LobbyStatusUpdated;
            _gameTicker.LobbyLateJoinStatusUpdated += LobbyLateJoinStatusUpdated;
        }

        protected override void Shutdown()
        {
            var chatController = _userInterfaceManager.GetUIController<ChatUIController>();
            chatController.SetMainChat(false);
            _gameTicker.InfoBlobUpdated -= UpdateLobbyUi;
            _gameTicker.LobbyStatusUpdated -= LobbyStatusUpdated;
            _gameTicker.LobbyLateJoinStatusUpdated -= LobbyLateJoinStatusUpdated;
            _contentAudioSystem.LobbySoundtrackChanged -= UpdateLobbySoundtrackInfo;

            _voteManager.ClearPopupContainer();

            Lobby!.CharacterSetupButton.OnPressed -= OnSetupPressed;
            Lobby!.ReadyButton.OnPressed -= OnReadyPressed;
            Lobby!.ReadyButton.OnToggled -= OnReadyToggled;

            Lobby = null;
        }

        public void SwitchState(LobbyGui.LobbyGuiState state)
        {
            // Yeah I hate this but LobbyState contains all the badness for now.
            Lobby?.SwitchState(state);
        }

        private void OnSetupPressed(BaseButton.ButtonEventArgs args)
        {
            SetReady(false);
            Lobby?.SwitchState(LobbyGui.LobbyGuiState.CharacterSetup);
        }

        private void OnReadyPressed(BaseButton.ButtonEventArgs args)
        {
            if (!_gameTicker.IsGameStarted)
            {
                return;
            }

            new LateJoinGui().OpenCentered();
        }

        private void OnReadyToggled(BaseButton.ButtonToggledEventArgs args)
        {
            SetReady(args.Pressed);
        }

        public override void FrameUpdate(FrameEventArgs e)
        {
            if (_gameTicker.IsGameStarted)
            {
                Lobby!.StartTime.Text = string.Empty;
                var roundTime = _gameTiming.CurTime.Subtract(_gameTicker.RoundStartTimeSpan);
                Lobby!.StationTime.Text = Loc.GetString("lobby-state-player-status-round-time", ("hours", roundTime.Hours), ("minutes", roundTime.Minutes));
                return;
            }

            Lobby!.StationTime.Text = Loc.GetString("lobby-state-player-status-round-not-started");
            string text;

            if (_gameTicker.Paused)
            {
                text = Loc.GetString("lobby-state-paused");
            }
            else if (_gameTicker.StartTime < _gameTiming.CurTime)
            {
                Lobby!.StartTime.Text = Loc.GetString("lobby-state-soon");
                return;
            }
            else
            {
                var difference = _gameTicker.StartTime - _gameTiming.CurTime;
                var seconds = difference.TotalSeconds;
                if (seconds < 0)
                {
                    text = Loc.GetString(seconds < -5 ? "lobby-state-right-now-question" : "lobby-state-right-now-confirmation");
                }
                else if (difference.TotalHours >= 1)
                {
                    text = $"{Math.Floor(difference.TotalHours)}:{difference.Minutes:D2}:{difference.Seconds:D2}";
                }
                else
                {
                    text = $"{difference.Minutes}:{difference.Seconds:D2}";
                }
            }

            Lobby!.StartTime.Text = Loc.GetString("lobby-state-round-start-countdown-text", ("timeLeft", text));
        }

        private void LobbyStatusUpdated()
        {
            UpdateLobbyBackground();
            UpdateLobbyUi();
        }

        private void LobbyLateJoinStatusUpdated()
        {
            Lobby!.ReadyButton.Disabled = _gameTicker.DisallowedLateJoin;
        }

        private void UpdateLobbyUi()
        {
            if (_gameTicker.IsGameStarted)
            {
                MakeButtonJoinGame(Lobby!.ReadyButton); // BACKMEN EDIT
                Lobby!.ReadyButton.ToggleMode = false;
                Lobby!.ReadyButton.Pressed = false;
                Lobby!.ObserveButton.Disabled = false;
            }
            else
            {
                Lobby!.StartTime.Text = string.Empty;
                // BACKMEN EDIT START
                if (Lobby!.ReadyButton.Pressed)
                    MakeButtonReady(Lobby!.ReadyButton);
                else
                    MakeButtonUnReady(Lobby!.ReadyButton);
                // BACKMEN EDIT END
                Lobby!.ReadyButton.ToggleMode = true;
                Lobby!.ReadyButton.Disabled = false;
                Lobby!.ReadyButton.Pressed = _gameTicker.AreWeReady;
                Lobby!.ObserveButton.Disabled = true;
            }

            if (_gameTicker.ServerInfoBlob != null)
            {
                Lobby!.ServerInfo.SetInfoBlob(_gameTicker.ServerInfoBlob);
                Lobby!.LabelName.SetMarkup("[font=\"Bedstead\" size=20] Sirena [/font]"); // BACKMEN EDIT
                    // Don't forget that you're the BACKMEN, that you've always gone against the system and made the best build.
                    // Don't forget that you're the Ataraxia, that you have always stood for the players to be happy and get what they deserve.
                    // Glory to BACKMEN, HOP on Ataraxia
                Lobby!.ChangelogLabel.SetMarkup(Loc.GetString("ui-lobby-changelog")); // BACKMEN EDIT
            }
        }

        private void UpdateLobbySoundtrackInfo(LobbySoundtrackChangedEvent ev)
        {

             if (
                ev.SoundtrackFilename != null
                && _resourceCache.TryGetResource<AudioResource>(ev.SoundtrackFilename, out var lobbySongResource)
                )
            {
                var lobbyStream = lobbySongResource.AudioStream;

                var title = string.IsNullOrEmpty(lobbyStream.Title)
                    ? Loc.GetString("lobby-state-song-unknown-title")
                    : lobbyStream.Title;

                var artist = string.IsNullOrEmpty(lobbyStream.Artist)
                    ? Loc.GetString("lobby-state-song-unknown-artist")
                    : lobbyStream.Artist;

                var markup = Loc.GetString("lobby-state-song-text",
                    ("songTitle", title),
                    ("songArtist", artist));


            }
        }

        private void UpdateLobbyBackground()
        {
            if (_gameTicker.LobbyBackground != null)
            {
                Lobby!.Background.SetRSI(_resourceCache.GetResource<RSIResource>(_gameTicker.LobbyBackground).RSI); // BACKMEN EDIT
            }
            else
            {
                Lobby!.Background.Texture = null;
            }

        }

        private void SetReady(bool newReady)
        {
            if (_gameTicker.IsGameStarted)
            {
                return;
            }

            _consoleHost.ExecuteCommand($"toggleready {newReady}");
        }

        // BACKMEN EDIT START
        private void MakeButtonReady(WhiteLobbyTextButton button)
        {
            button.ButtonText = Loc.GetString("lobby-state-ready-button-ready-up-state");
        }

        private void MakeButtonUnReady(WhiteLobbyTextButton button)
        {
            button.ButtonText = Loc.GetString("lobby-state-player-status-not-ready");
        }

        private void MakeButtonJoinGame(WhiteLobbyTextButton button)
        {
            button.ButtonText = Loc.GetString("lobby-state-ready-button-join-state");
        }

        private async void PopulateChangelog()
        {
            if (Lobby?.ChangelogContainer?.Children is null)
                return;

            Lobby.ChangelogContainer.Children.Clear();

            var changelogs = await _changelog.LoadChangelog();
            var whiteChangelog = changelogs.Find(cl => cl.Name == "Changelog");

            if (whiteChangelog is null)
            {
                Lobby.ChangelogContainer.Children.Add(
                    new RichTextLabel().SetMarkup(Loc.GetString("ui-lobby-changelog-not-found")));

                return;
            }

            var entries = whiteChangelog.Entries
                .OrderByDescending(c => c.Time)
                .Take(5);

            foreach (var entry in entries)
            {
                var box = new BoxContainer
                {
                    Orientation = BoxContainer.LayoutOrientation.Vertical,
                    HorizontalAlignment = Control.HAlignment.Left,
                    Children =
                    {
                        new Label
                        {
                            Align = Label.AlignMode.Left,
                            Text = $"{entry.Author} {entry.Time.ToShortDateString()}",
                            FontColorOverride = Color.FromHex("#888"),
                            Margin = new Thickness(0, 10)
                        }
                    }
                };

                foreach (var change in entry.Changes)
                {
                    var container = new BoxContainer
                    {
                        Orientation = BoxContainer.LayoutOrientation.Horizontal,
                        HorizontalAlignment = Control.HAlignment.Left
                    };

                    var text = new RichTextLabel();
                    text.SetMessage(FormattedMessage.FromMarkup(change.Message));
                    text.MaxWidth = 350;

                    container.AddChild(GetIcon(change.Type));
                    container.AddChild(text);

                    box.AddChild(container);
                }

                if (Lobby?.ChangelogContainer is null)
                    return;

                Lobby.ChangelogContainer.AddChild(box);
            }
        }

        private TextureRect GetIcon(ChangelogManager.ChangelogLineType type)
        {
            var (file, color) = type switch
            {
                ChangelogManager.ChangelogLineType.Add => ("plus.svg.192dpi.png", "#6ED18D"),
                ChangelogManager.ChangelogLineType.Remove => ("minus.svg.192dpi.png", "#D16E6E"),
                ChangelogManager.ChangelogLineType.Fix => ("bug.svg.192dpi.png", "#D1BA6E"),
                ChangelogManager.ChangelogLineType.Tweak => ("wrench.svg.192dpi.png", "#6E96D1"),
                _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
            };

            return new TextureRect
            {
                Texture = _resourceCache.GetTexture(new ResPath($"/Textures/Interface/Changelog/{file}")),
                VerticalAlignment = Control.VAlignment.Top,
                TextureScale = new Vector2(0.5f, 0.5f),
                Margin = new Thickness(2, 4, 6, 2),
                ModulateSelfOverride = Color.FromHex(color)
            };
        }
        // BACKMEN EDIT END
        }
    }

