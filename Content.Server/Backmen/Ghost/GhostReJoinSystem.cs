using System.Linq;
using Content.Server.Chat.Managers;
using Content.Server.EUI;
using Content.Server.GameTicking;
using Content.Server.Players.PlayTimeTracking;
using Content.Server.Preferences.Managers;
using Content.Server.Station.Components;
using Content.Server.Station.Systems;
using Content.Shared.Administration;
using Content.Shared.Administration.Logs;
using Content.Shared.Backmen.CCVar;
using Content.Shared.Backmen.Ghost;
using Content.Shared.Database;
using Content.Shared.GameTicking;
using Content.Shared.Ghost;
using Content.Shared.Humanoid;
using Content.Shared.Mind;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Content.Shared.Roles.Jobs;
using Robust.Server.Player;
using Robust.Shared.Console;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Server.Backmen.Ghost;

public sealed class GhostReJoinSystem : SharedGhostReJoinSystem
{
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly ISharedAdminLogManager _adminLogger = default!;
    [Dependency] private readonly GameTicker _gameTicker = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IChatManager _chatManager = default!;
    [Dependency] private readonly IConsoleHost _console = default!;
    [Dependency] private readonly SharedGhostSystem _ghostSystem = default!;
    [Dependency] private readonly EuiManager _euiManager = default!;
    [Dependency] private readonly IServerPreferencesManager _prefs = default!;
    [Dependency] private readonly ActorSystem _actorSystem = default!;
    [Dependency] private readonly SharedRoleSystem _roles = default!;
    [Dependency] private readonly PlayTimeTrackingSystem _playTimeTrackings = default!;
    [Dependency] private readonly SharedHumanoidAppearanceSystem _appearance = default!;
    [Dependency] private readonly StationJobsSystem _stationJobs = default!;
    [Dependency] private readonly SharedJobSystem _jobs = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RoundRestartCleanupEvent>(ResetDeathTimes);
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(HandleUserCharacter);

        _configurationManager.OnValueChanged(CCVars.GhostRespawnMaxPlayers,
            ghostRespawnMaxPlayers =>
            {
                _ghostRespawnMaxPlayers = ghostRespawnMaxPlayers;
            },
            true);


        _console.RegisterCommand("bkm_return_to_round", ReturnToRoundCommand, ReturnToRoundCompletion);
    }

    private void HandleUserCharacter(PlayerSpawnCompleteEvent ev)
    {
        _usedInRound.TryAdd(ev.Player.UserId, []);
        var list = _usedInRound[ev.Player.UserId];
        var prefs = _prefs.GetPreferencesOrNull(ev.Player.UserId);
        if (prefs == null)
        {
            Log.Error($"GetPreferences returned null for player {ev.Player}");
            return;
        }

        if (!prefs.TryIndexOfCharacter(ev.Profile, out var idx))
        {
            Log.Warning($"GetPreferences returned no character for player {ev.Player}");
            return;
        }

        list.Add(idx);
    }

    public void OnJoinSelected(GhostReJoinEui ui, Entity<GhostComponent> ent, ref GhostReJoinCharacterMessage args)
    {
        if (TerminatingOrDeleted(ent))
        {
            _euiManager.CloseEui(ui);
            return;
        }

        if (_playerManager.PlayerCount >= _ghostRespawnMaxPlayers)
        {
            SendChatMsg(ui.Player,
                Loc.GetString("ghost-respawn-max-players", ("players", _ghostRespawnMaxPlayers))
            );
            return;
        }

        var station = GetEntity(args.Station);
        if (!TryComp<StationJobsComponent>(station, out var jobs))
        {
            return;
        }
        var timeOffset = _gameTiming.CurTime - ent.Comp.TimeOfDeath;
        if (timeOffset < _ghostRespawnTime)
        {
            SendChatMsg(ui.Player,
                Loc.GetString("ghost-respawn-time-left", ("time", (_ghostRespawnTime - timeOffset).ToString()))
            );
            return;
        }

        var prefs = _prefs.GetPreferencesOrNull(ui.Player.UserId);
        if(prefs == null)
            return;

        if(!prefs.Characters.TryGetValue(args.Id, out var character))
            return;

        _euiManager.CloseEui(ui);
        _eUi.Remove(ui.Player.UserId);
        _deathTime.Remove(ui.Player.UserId);

        SpawnPlayerOnStation((station,jobs),ui.Player, (HumanoidCharacterProfile)character);
    }

    public void OnJoinRandom(GhostReJoinEui ui, Entity<GhostComponent> ent, ref GhostReJoinRandomMessage args)
    {
        if (TerminatingOrDeleted(ent))
        {
            _euiManager.CloseEui(ui);
            return;
        }

        if (_playerManager.PlayerCount >= _ghostRespawnMaxPlayers)
        {
            SendChatMsg(ui.Player,
                Loc.GetString("ghost-respawn-max-players", ("players", _ghostRespawnMaxPlayers))
            );
            return;
        }

        var station = GetEntity(args.Station);
        if (!TryComp<StationJobsComponent>(station, out var jobs))
        {
            return;
        }
        var timeOffset = _gameTiming.CurTime - ent.Comp.TimeOfDeath;
        if (timeOffset < _ghostRespawnTime)
        {
            SendChatMsg(ui.Player,
                Loc.GetString("ghost-respawn-time-left", ("time", (_ghostRespawnTime - timeOffset).ToString()))
            );
            return;
        }

        var character = HumanoidCharacterProfile.Random();

        _euiManager.CloseEui(ui);
        _eUi.Remove(ui.Player.UserId);
        _deathTime.Remove(ui.Player.UserId);

        SpawnPlayerOnStation((station,jobs),ui.Player, character);
    }

    private void SpawnPlayerOnStation(Entity<StationJobsComponent> station, ICommonSession player, HumanoidCharacterProfile character)
    {
        _deathTime.Remove(player.UserId);

        var jobPrototype = _prototype.Index(station.Comp.OverflowJobs.First());


        var newMind = _mind.CreateMind(player.UserId, character.Name);
        _mind.SetUserId(newMind, player.UserId);
        _roles.MindAddJobRole(newMind, silent: false, jobPrototype:jobPrototype.ID);

        _playTimeTrackings.PlayerRolesChanged(player);

        var spawnEv = new PlayerSpawningEvent(jobPrototype.ID, character, station);
        RaiseLocalEvent(spawnEv);
        DebugTools.Assert(spawnEv.SpawnResult is { Valid: true } or null);

        var mob = spawnEv.SpawnResult!.Value;
        _appearance.LoadProfile(mob, character);
        _mind.TransferTo(newMind, mob);
        _stationJobs.TryAssignJob(station, jobPrototype, player.UserId);

        var jobName = _jobs.MindTryGetJobName(newMind);
        _adminLogger.Add(LogType.Mind,
            LogImpact.Extreme,
            $"гост респавн {player.Name} late joined as {character.Name:characterName} on station {Name(station):stationName} with {ToPrettyString(mob):entity} as a {jobName:jobNam}.");

        if (TryComp(station, out MetaDataComponent? metaData))
        {
            _chatManager.DispatchServerMessage(player,
                Loc.GetString("job-greet-station-name", ("stationName", metaData.EntityName)));
        }

        var ev = new PlayerSpawnCompleteEvent(mob, player, jobPrototype.ID, true, true, 0, station, character);
        RaiseLocalEvent(ev);

        SendChatMsg(player,
            Loc.GetString("ghost-respawn-window-rules-footer")
        );
    }

    private List<EntityUid> GetSpawnableStations()
    {
        var spawnableStations = new List<EntityUid>();
        var query = EntityQueryEnumerator<StationJobsComponent, StationSpawningComponent, MetaDataComponent>();
        while (query.MoveNext(out var uid, out var stationJobsComponent, out _, out var metaDataComponent))
        {
            if(Paused(uid, metaDataComponent))
                continue;
            if(stationJobsComponent.OverflowJobs.Count == 0)
                continue;
            spawnableStations.Add(uid);
        }

        return spawnableStations;
    }

    public GhostReJoinInterfaceState UpdateUserInterface(Entity<GhostComponent> ent)
    {
        var msg = new GhostReJoinInterfaceState();

        if(!_actorSystem.TryGetSession(ent, out var session))
            return msg;

        var prefs = _prefs.GetPreferencesOrNull(session!.UserId);

        if(prefs == null)
            return msg;

        _usedInRound.TryAdd(session.UserId, []);
        var usedByUser = _usedInRound[session.UserId];

        msg.Characters.AddRange(
            prefs.Characters
                .Where(x => !usedByUser.Contains(x.Key))
                .Select(x => new GhostReJoinCharacter(x.Key, x.Value.Name))
            );

        msg.Stations.AddRange(
            GetSpawnableStations().Select(s => new GhostReJoinStation(GetNetEntity(s), Name(s)))
            );

        return msg;
    }

    private CompletionResult ReturnToRoundCompletion(IConsoleShell shell, string[] args)
    {
        return CompletionResult.Empty;
    }

    [AnyCommand]
    private void ReturnToRoundCommand(IConsoleShell shell, string argstr, string[] args)
    {
        if (shell.Player?.AttachedEntity is not { } ghost || !TryComp<GhostComponent>(ghost, out var ghostComponent))
        {
            shell.WriteError("This command can only be ran by a player with an attached entity.");
            return;
        }

        if (_playerManager.PlayerCount >= _ghostRespawnMaxPlayers)
        {
            SendChatMsg(shell.Player,
                Loc.GetString("ghost-respawn-max-players", ("players", _ghostRespawnMaxPlayers))
            );
            return;
        }

        var userId = shell.Player.UserId;

        if (!_deathTime.TryGetValue(userId, out var deathTime))
        {
            _deathTime[userId] = ghostComponent.TimeOfDeath;
            deathTime = ghostComponent.TimeOfDeath;
        }

        if (deathTime != ghostComponent.TimeOfDeath)
        {
            _ghostSystem.SetTimeOfDeath(ghost, deathTime, ghostComponent);
            Dirty(ghost, ghostComponent);
        }

        var timeOffset = _gameTiming.CurTime - deathTime;

        if (timeOffset >= _ghostRespawnTime)
        {
            if (_eUi.ContainsKey(userId))
            {
                _euiManager.CloseEui(_eUi[userId]);
                _eUi.Remove(userId);
            }
            _eUi.Add(userId, new GhostReJoinEui(this, (ghost, ghostComponent)));
            _euiManager.OpenEui(_eUi[userId], shell.Player);
            _eUi[userId].StateDirty();

            return;
        }

        SendChatMsg(shell.Player,
            Loc.GetString("ghost-respawn-time-left", ("time", (_ghostRespawnTime - timeOffset).ToString()))
        );
    }

    private int _ghostRespawnMaxPlayers;
    private readonly Dictionary<NetUserId, TimeSpan> _deathTime = new();
    private readonly Dictionary<NetUserId, HashSet<int>> _usedInRound = new();
    private readonly Dictionary<NetUserId, GhostReJoinEui> _eUi = new();

    private void ResetDeathTimes(RoundRestartCleanupEvent ev)
    {
        _deathTime.Clear();
        _usedInRound.Clear();
        _eUi.Clear();
    }

    private void SendChatMsg(ICommonSession sess, string message)
    {
        _chatManager.ChatMessageToOne(Shared.Chat.ChatChannel.Server,
            message,
            Loc.GetString("chat-manager-server-wrap-message", ("message", message)),
            default,
            false,
            sess.Channel,
            Color.Red);
    }

    public void AttachGhost(EntityUid ghost, ICommonSession? mindSession)
    {
        if(mindSession == null)
            return;

        if(!_deathTime.ContainsKey(mindSession.UserId))
            _deathTime[mindSession.UserId] = _gameTiming.CurTime;

        Log.Debug($"Attach time {_deathTime[mindSession.UserId]} to ghost {ghost:entity}");

        if (TryComp<GhostComponent>(ghost, out var ghostComponent))
        {
            _ghostSystem.SetTimeOfDeath(ghost, _deathTime[mindSession.UserId], ghostComponent);
            Dirty(ghost, ghostComponent);
        }
    }
}
