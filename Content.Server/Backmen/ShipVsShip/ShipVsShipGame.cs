using System.Linq;
using Content.Server.Backmen.Arrivals;
using Content.Server.Backmen.RoleWhitelist;
using Content.Server.Backmen.ShipVsShip.Components;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Rules;
using Content.Server.Maps;
using Content.Server.RoundEnd;
using Content.Server.Shuttles.Events;
using Content.Server.Shuttles.Systems;
using Content.Server.Spawners.Components;
using Content.Server.Station.Components;
using Content.Server.Station.Systems;
using Content.Shared.Backmen.Teams;
using Content.Shared.Backmen.Teams.Components;
using Content.Shared.Construction.Components;
using Content.Shared.Destructible;
using Content.Shared.GameTicking;
using Content.Shared.GameTicking.Components;
using Content.Shared.Mind;
using Content.Shared.Mobs;
using Content.Shared.Roles;
using Content.Shared.Roles.Jobs;
using Content.Shared.Shuttles.Components;
using Content.Shared.Timing;
using Robust.Server.Containers;
using Robust.Shared.Map.Components;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server.Backmen.ShipVsShip;

public sealed class ShipVsShipGame : GameRuleSystem<ShipVsShipGameComponent>
{
    //private ISawmill _sawmill = default!;
    //[Dependency] private readonly StationSystem _stationSystem = default!;
    [Dependency] private readonly WhitelistSystem _whitelistSystem = default!;
    [Dependency] private readonly StationJobsSystem _stationJobs = default!;
    [Dependency] private readonly ISharedPlayerManager _playerManager = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly StationSpawningSystem _stationSpawning = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly ShuttleConsoleSystem _console = default!;
    [Dependency] private readonly RoundEndSystem _endSystem = default!;
    [Dependency] private readonly IGameTiming _gameTiming = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedTdmTeamSystem _teamSystem = default!;
    [Dependency] private readonly ContainerSystem _container = default!;
    [Dependency] private readonly SharedRoleSystem _roleSystem = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RulePlayerSpawningEvent>(OnPlayersSpawned);
        SubscribeLocalEvent<PlayerBeforeSpawnEvent>(OnBeforeSpawn);
        SubscribeLocalEvent<SVSTeamCoreComponent, MobStateChangedEvent>(OnChangeHealth);
        SubscribeLocalEvent<SVSTeamCoreComponent, DestructionEventArgs>(OnDestroy);
        SubscribeLocalEvent<LoadingMapsEvent>(OnLoadMap);
        SubscribeLocalEvent<FTLCompletedEvent>(OnAfterFtl);
        SubscribeLocalEvent<RoundStartedEvent>(OnStartRound);
        SubscribeLocalEvent<CanHandleWithArrival>(CanUseArrivals);
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnAfterSpawning);
    }

    protected override void AppendRoundEndText(EntityUid uid, ShipVsShipGameComponent rule, GameRuleComponent gameRule,
        ref RoundEndTextAppendEvent args)
    {
        args.AddLine(Loc.GetString($"svs-team-{rule.Winner ?? StationTeamMarker.Neutral}-lose", ("target",rule.WinnerTarget ?? EntityUid.Invalid)));
    }

    private void SetFlag(EntityUid ent, StationTeamMarker team)
    {
        _teamSystem.SetFaction((ent,EnsureComp<TdmMemberComponent>(ent)), team);
    }

    [ValidatePrototypeId<EntityPrototype>]
    private const string PlayerStationAi = "PlayerStationAiSAI";
    [ValidatePrototypeId<EntityPrototype>]
    private const string StationAiBrain = "StationAiBrain";
    [ValidatePrototypeId<JobPrototype>]
    private const string StationAiJob = "SAIShip";
    private EntityUid FixStationAi(EntityUid mob)
    {
        if(Prototype(mob)?.ID != StationAiBrain)
            return mob;

        var xform = Transform(mob);
        if (!TryComp<MapGridComponent>(xform.GridUid, out var grid))
            return mob;

        var uid = EntityUid.Invalid;
        foreach (var entityUid in _map.GetAnchoredEntities(xform.GridUid.Value, grid, xform.Coordinates))
        {
            if(!HasComp<ContainerSpawnPointComponent>(entityUid))
                continue;
            uid = entityUid;
            break;
        }

        if(!uid.Valid)
            uid = SpawnAtPosition(PlayerStationAi, xform.Coordinates);

        var spawnPoint = EnsureComp<ContainerSpawnPointComponent>(uid);
        if (!_container.TryGetContainer(uid, spawnPoint.ContainerId, out var container))
        {
            QueueDel(uid);
            return mob;
        }
        if (!_container.Insert(mob, container))
        {
            QueueDel(uid);
            return mob;
        }

        RemCompDeferred<AnchorableComponent>(uid);
        return uid;
    }

    private void OnAfterSpawning(PlayerSpawnCompleteEvent ev)
    {
        if(!QueryActiveRules().MoveNext(out _, out var rule, out _))
            return;

        var mobEntity = ev.Mob;

        //rule.Players.TryAdd(StationTeamMarker.TeamA, []);
        //rule.Players.TryAdd(StationTeamMarker.TeamB, []);

        FixStationAi(mobEntity);

        var xform = Transform(mobEntity);
        var team = rule.Players.FirstOrNull(x => x.Value.Contains(ev.Player.UserId))?.Key;

        if (team == null)
        {
            var weakTeam = rule.Players.MinBy(x => x.Value.Count);
            team = weakTeam.Key;
            rule.Players[team.Value].Add(ev.Player.UserId);
        }

        Log.Info($"Validate player spawning station {mobEntity:entity} on {xform.GridUid:entity} (team: {team})");


        SetFlag(mobEntity, team.Value);
        if (!TryComp<StationDataComponent>(rule.Team[team.Value], out var stationDataComponent) ||
            !rule.Team.ContainsKey(team.Value))
            return;

        var stationGrids = stationDataComponent.Grids;
        if (xform.GridUid == null || stationGrids.Contains(xform.GridUid.Value))
        {
            return;
        }
        var latejoin = (from s in EntityQuery<SpawnPointComponent, TransformComponent>()
            where s.Item1.SpawnType == SpawnPointType.LateJoin && s.Item2.GridUid.HasValue && stationGrids.Contains(s.Item2.GridUid.Value)
            select s.Item2.Coordinates).ToList();
        if (latejoin.Count == 0)
        {
            Log.Error($"not found late join for {team}");
            return;
        }

        var point = RobustRandom.Pick(latejoin);
        _transform.SetCoordinates(mobEntity, point);
        Log.Warning($"Invalid spawning station {mobEntity:entity} on {xform.GridUid:entity} (team: {team}) do fixing, new grid = {point.EntityId:entity}");
    }

    private void CanUseArrivals(CanHandleWithArrival ev)
    {
        var activeRules = QueryActiveRules();

        while (activeRules.MoveNext(out _, out _, out _))
        {
            ev.Cancel();
        }
    }

    private void OnStartRound(RoundStartedEvent ev)
    {
        if(!QueryActiveRules().MoveNext(out _, out var rule, out _))
            return;

        ScanForObjects(rule);
    }

    private void ScanForObjects(ShipVsShipGameComponent rule)
    {
        var q = EntityQueryEnumerator<StationDataComponent, StationJobsComponent, StationTeamMarkerComponent>();

        while (q.MoveNext(out var ent, out var stationDataComponent, out var stationJobsComponent,
                   out var stationTeamMarkerComponent))
        {
            var stationGrids = stationDataComponent.Grids;

            var team = stationTeamMarkerComponent.Team;
            rule.OverflowJobs.TryAdd(team, []);
            rule.Objective.TryAdd(team, []);
            foreach (var overflowJob in stationJobsComponent.OverflowJobs)
            {
                rule.OverflowJobs[team].Add(overflowJob);
            }

            var winQuery = EntityQueryEnumerator<MetaDataComponent, TransformComponent>();
            while (winQuery.MoveNext(out var owner, out var md, out var xform))
            {
                if (xform.GridUid == null)
                    continue; //in space

                if (!stationGrids.Contains(xform.GridUid.Value))
                    continue; // not in target station

                var proto = Prototype(owner, md);
                if (proto == null || !stationTeamMarkerComponent.Goal.Contains(proto.ID))
                    continue;

                rule.Objective[team].Add(owner);
                EnsureComp<SVSTeamCoreComponent>(owner);
            }
        }
    }

    private void OnAfterFtl(ref FTLCompletedEvent ev)
    {
        if(!QueryActiveRules().MoveNext(out _, out _, out _))
            return;

        EnsureComp<FTLComponent>(ev.Entity).StateTime = StartEndTime.FromCurTime(_gameTiming, 60 * 5);
        _console.RefreshShuttleConsoles(ev.Entity);
    }

    private void OnLoadMap(LoadingMapsEvent ev)
    {
        if (GameTicker.CurrentPreset?.ID != "ShipVsShip")
            return;

        var mainStationMap = ev.Maps.FirstOrDefault();

        if (mainStationMap != null && GameTicker.CurrentPreset?.MapPool != null &&
            _prototypeManager.TryIndex<GameMapPoolPrototype>(GameTicker.CurrentPreset.MapPool, out var pool) &&
            !pool.Maps.Contains(mainStationMap!.ID))
        {

            foreach (var map in pool.Maps)
            {
                if(ev.Maps.Any(x=>x.ID == map))
                    continue;

                ev.Maps.Clear();
                ev.Maps.Add(_prototypeManager.Index<GameMapPrototype>(RobustRandom.Pick(pool.Maps)));
            }
        }
    }

    private void CheckEnd(EntityUid ent)
    {
        if (!QueryActiveRules().MoveNext(out var ruleUid, out var r1, out var rule, out var r3))
        {
            return;
        }

        if (GameTicker.RunLevel != GameRunLevel.InRound)
            return;

        var team = rule.Objective.FirstOrNull(x => x.Value.Contains(ent))?.Key ?? StationTeamMarker.Neutral;
        if (team == StationTeamMarker.Neutral)
            return;

                /*
        if (TryComp<StationDataComponent>(rule.Team[team], out var stationDataComponent))
        {
            foreach (var grid in stationDataComponent.Grids)
            {
                QueueDel(grid);
            }
        }*/

        rule.Winner = team;
        rule.WinnerTarget = ent;
        _endSystem.EndRound();

    }

    private void OnChangeHealth(Entity<SVSTeamCoreComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead)
            return;

        CheckEnd(ent);
    }

    private void OnDestroy(Entity<SVSTeamCoreComponent> ent, ref DestructionEventArgs args)
    {
        CheckEnd(ent);
    }

    private void OnBeforeSpawn(PlayerBeforeSpawnEvent ev)
    {
        if(!QueryActiveRules().MoveNext(out var ruleUid, out var r1, out var rule, out var r3))
            return;

        var team = rule.Players.FirstOrDefault(x => x.Value.Contains(ev.Player.UserId)).Key;

        if (!rule.Team.ContainsKey(team))
        {
            return;
        }

        if (ev.Station != rule.Team[team])
        {
            ev.Handled = true;
            var newMind = _mind.CreateMind(ev.Player.UserId, ev.Profile.Name);
            _mind.SetUserId(newMind, ev.Player.UserId);


            var job =  RobustRandom.Pick(rule.OverflowJobs[team]);
            _roleSystem.MindAddRole(newMind, job.Id);
            _roleSystem.MindHasRole<JobRoleComponent>(newMind!, out var jobRole);

            var mobMaybe = _stationSpawning.SpawnPlayerCharacterOnStation(rule.Team[team], jobRole?.Comp1.JobPrototype, ev.Profile);
            DebugTools.AssertNotNull(mobMaybe);
            var mob = mobMaybe!.Value;
            SetFlag(mob, team);
            _mind.TransferTo(newMind, mob);
            return; // invalid team? skip
        }
    }

    protected override void Added(EntityUid uid, ShipVsShipGameComponent component, GameRuleComponent gameRule,
        GameRuleAddedEvent args)
    {
        var activeRules = QueryActiveRules();

        while (activeRules.MoveNext(out var ruleUid, out var r1, out var r2, out var r3))
        {
            if (ruleUid == uid)
                continue;

            GameTicker.EndGameRule(uid, gameRule);
            return;
        }
    }

    private void OnPlayersSpawned(RulePlayerSpawningEvent ev)
    {
        if (!QueryActiveRules().MoveNext(out var ruleId, out _, out var rule, out var ruleData))
        {
            return;
        }

        var teams = EntityQuery<MetaDataComponent, StationTeamMarkerComponent, StationJobsComponent>(true)
            .ToDictionary(x => x.Item2.Team, x => x);

        var ct = new Dictionary<StationTeamMarker, uint>();

        var teamStation = teams.ToDictionary(x => x.Key, x => x.Value.Item1.Owner);
        rule.Team = teamStation;

        // faction init
        foreach (var (fraction, _) in teamStation)
        {
            rule.Players.TryAdd(fraction, []);
            ct.TryAdd(fraction, 0);
        }

        var playerInRole = new Dictionary<NetUserId, (string?, EntityUid)>();

        // Капитан, Сай
        foreach (var (team, (md, marker, jobs)) in teams)
        {
            var stationUid = teamStation[team];

            var assign = _stationJobs
                .AssignJobs(ev.Profiles.Where(x => !playerInRole.ContainsKey(x.Key)).ToDictionary(),
                    new[] { stationUid })
                .ToDictionary(x => x.Key, x => x.Value.Item1);

            #region required jobs
            foreach (var job in marker.RequireJobs)
            {
                var user = assign.FirstOrNull(x => x.Value == job.Id);
                if (user == null)
                {
                    NetUserId? pickedUser = null;
                    {
                        var listPicks = ev.Profiles.Keys.Where(x =>
                            !playerInRole.ContainsKey(x) && _whitelistSystem.IsInWhitelist(x)).ToList();
                        if (listPicks.Count > 0)
                        {
                            pickedUser = RobustRandom.Pick(listPicks);
                        }
                    }
                    if (pickedUser == null)
                    {
                        var listPicks = ev.Profiles.Keys.Where(x =>
                            !playerInRole.ContainsKey(x)).ToList();

                        if (listPicks.Count == 0)
                        {
                            continue; // skip team no more players
                        }

                        pickedUser = RobustRandom.Pick(listPicks);
                    }

                    ct[team]++;
                    playerInRole.Add(pickedUser.Value, (job, stationUid));
                    assign.Remove(pickedUser.Value);
                    continue;
                }

                ct[team]++;
                playerInRole.Add(user.Value.Key, (job, stationUid));
                assign.Remove(user.Value.Key);
            }
            #endregion

            var overflowJobs = jobs.OverflowJobs;
            foreach (var (user, job) in assign)
            {
                if (job == null || overflowJobs.Contains(job.Value.Id))
                    continue;

                ct[team]++;
                playerInRole.Add(user, (job, stationUid));
            }
        }

        if (ct.Count == 0)
        {
            GameTicker.EndGameRule(ruleId,ruleData);
            GameTicker.EndRound("не правильная карта!");
            return;
        }

        // overflow
        foreach (var user in ev.Profiles.Keys.Where(x => !playerInRole.ContainsKey(x)).ToArray())
        {
            var weakTeam = ct.MinBy(x => x.Value).Key;
            var jobs = teams[weakTeam].Item3.OverflowJobs;
            var job = RobustRandom.Pick(jobs);

            ct[weakTeam]++;
            playerInRole.Add(user, (job, teamStation[weakTeam]));
        }

        // ai fix
        var aiQ = EntityQueryEnumerator<SpawnPointComponent,TransformComponent>();
        while (aiQ.MoveNext(out var owner,out var container, out var xform))
        {
            if(container.Job != StationAiJob)
                continue;

            var stationUid = _station.GetOwningStation(owner, xform);
            var uid = SpawnAtPosition(PlayerStationAi, xform.Coordinates);
            var team = teamStation.FirstOrNull(x => x.Value == stationUid)?.Key ?? StationTeamMarker.Neutral;
            EnsureComp<ContainerSpawnPointComponent>(uid);
            SetFlag(uid, team);
        }

        foreach (var (player, (job, station)) in playerInRole)
        {
            if (job == null)
                continue;

            var sess = _playerManager.GetSessionById(player);
            ev.PlayerPool.Remove(sess);

            var team = teamStation.FirstOrNull(x => x.Value == station)?.Key ?? StationTeamMarker.Neutral;

            rule.Players.TryAdd(team, new HashSet<NetUserId>());
            rule.Players[team].Add(player);

            GameTicker.SpawnPlayer(sess, ev.Profiles[player], station, job, false);
            // continue in OnBeforeSpawn
        }
    }

    protected override void Started(EntityUid uid, ShipVsShipGameComponent rule, GameRuleComponent ruleGame, GameRuleStartedEvent args)
    {
        ScanForObjects(rule);
    }
}
