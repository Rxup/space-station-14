using System.Linq;
using Content.Server.Actions;
using Content.Server.Chat.Managers;
using Content.Server.Backmen.Flesh;
using Content.Server.GameTicking.Rules.Components;
using Content.Server.Mind;
using Content.Server.NPC.Components;
using Content.Server.Players;
using Content.Server.Roles;
using Content.Server.NPC.Systems;
using Content.Server.Objectives;
using Content.Server.Radio.Components;
using Content.Server.RoundEnd;
using Content.Server.Shuttles.Components;
using Content.Server.Station.Components;
using Content.Server.Station.Systems;
using Content.Server.Store.Components;
using Content.Server.Store.Systems;
using Content.Shared.Backmen.Abilities.Psionics;
using Content.Shared.Backmen.CCVar;
using Content.Shared.FixedPoint;
using Content.Shared.Backmen.Flesh;
using Content.Shared.Humanoid;
using Content.Shared.Mind;
using Content.Shared.Objectives.Components;
using Content.Shared.Players;
using Content.Shared.Preferences;
using Content.Shared.Radio;
using Content.Shared.Roles;
using Content.Shared.Roles.Jobs;
using Robust.Server.GameObjects;
using Robust.Server.Player;
using Robust.Shared.Audio;
using Robust.Shared.Configuration;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Utility;

namespace Content.Server.GameTicking.Rules;

public sealed class FleshCultRuleSystem : GameRuleSystem<FleshCultRuleComponent>
{
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IChatManager _chatManager = default!;
    [Dependency] private readonly NpcFactionSystem _faction = default!;
    [Dependency] private readonly SharedAudioSystem _audioSystem = default!;
    [Dependency] private readonly RoundEndSystem _roundEndSystem = default!;
    [Dependency] private readonly StationSystem _stationSystem = default!;
    [Dependency] private readonly StoreSystem _store = default!;
    [Dependency] private readonly MindSystem _mindSystem = default!;
    [Dependency] private readonly ActorSystem _actorSystem = default!;
    [Dependency] private readonly RoleSystem _roleSystem = default!;
    [Dependency] private readonly SharedJobSystem _jobs = default!;
    [Dependency] private readonly ObjectivesSystem _objectivesSystem = default!;

    private ISawmill _sawmill = default!;

    private int PlayersPerCultist => _cfg.GetCVar(CCVars.FleshCultPlayersPerCultist);
    private int MaxCultists => _cfg.GetCVar(CCVars.FleshCultMaxCultist);

    public override void Initialize()
    {
        base.Initialize();

        _sawmill = Logger.GetSawmill("preset");

        SubscribeLocalEvent<RoundStartAttemptEvent>(OnStartAttempt);
        SubscribeLocalEvent<RulePlayerJobsAssignedEvent>(OnPlayersSpawned);
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(HandleLatejoin);
        SubscribeLocalEvent<RoundEndTextAppendEvent>(OnRoundEndText);
        SubscribeLocalEvent<FleshHeartSystem.FleshHeartFinalEvent>(OnFleshHeartFinal);
    }

    private void OnFleshHeartFinal(FleshHeartSystem.FleshHeartFinalEvent ev)
    {
        var query = EntityQueryEnumerator<FleshCultRuleComponent, GameRuleComponent>();
        _sawmill.Info("Get FleshHeartFinalEvent");
        while (query.MoveNext(out var uid, out var fleshCult, out var gameRule))
        {
            if (!GameTicker.IsGameRuleAdded(uid, gameRule))
            {
                _sawmill.Info("FleshCultRule not added");
                continue;
            }

            if (ev.OwningStation == null)
            {
                _sawmill.Info("OwningStation is null");
                return;
            }

            if (fleshCult.TargetStation == null)
            {
                _sawmill.Info("TargetStation is null");
                return;
            }

            _sawmill.Info(fleshCult.TargetStation.Value.ToString());

            if (!TryComp(fleshCult.TargetStation, out StationDataComponent? data))
            {
                _sawmill.Info("TargetStation not have StationDataComponent");
                return;
            }
            foreach (var grid in data.Grids)
            {
                if (grid != ev.OwningStation)
                {
                    _sawmill.Info("grid not be TargetStation");
                    continue;
                }

                _sawmill.Info("FleshHeart Win");
                fleshCult.WinType = FleshCultRuleComponent.WinTypes.FleshHeartFinal;
                _roundEndSystem.EndRound();
                return;
            }
        }
    }

    private void OnStartAttempt(RoundStartAttemptEvent ev)
    {
        var query = EntityQueryEnumerator<FleshCultRuleComponent, GameRuleComponent>();
        while (query.MoveNext(out var uid, out var fleshCult, out var gameRule))
        {
            if (!GameTicker.IsGameRuleAdded(uid, gameRule))
                continue;

            var minPlayers = _cfg.GetCVar(CCVars.FleshCultMinPlayers);
            if (!ev.Forced && ev.Players.Length < minPlayers)
            {
                _chatManager.DispatchServerAnnouncement(Loc.GetString("flesh-cult-not-enough-ready-players",
                    ("readyPlayersCount", ev.Players.Length), ("minimumPlayers", minPlayers)));
                ev.Cancel();
                continue;
            }

            if (ev.Players.Length == 0)
            {
                _chatManager.DispatchServerAnnouncement(Loc.GetString("flesh-cult-no-one-ready"));
                ev.Cancel();
            }
        }
    }

    private void DoCultistStart(FleshCultRuleComponent component)
    {
        if (!component.StartCandidates.Any())
        {
            _sawmill.Error("Tried to start FleshCult mode without any candidates.");
            return;
        }

        component.TargetStation = _stationSystem.GetStations().FirstOrNull();

        if (component.TargetStation == null)
        {
            _sawmill.Error("No found target station for flesh cult.");
            return;
        }

        var numCultists = MathHelper.Clamp(component.StartCandidates.Count / PlayersPerCultist, 1, MaxCultists);

        IPlayerSession? cultistsLeader = null;
        var cultistsLeaderPool = FindPotentialCultistsLeader(component.StartCandidates, component);
        if (cultistsLeaderPool.Count != 0)
        {
            cultistsLeader = _random.PickAndTake(cultistsLeaderPool);
            numCultists += -1;
        }

        var cultistsPool = FindPotentialCultists(component.StartCandidates, component);
        var selectedCultists = PickCultists(numCultists, cultistsPool);
        if (cultistsLeader != null)
        {
            selectedCultists.Remove(cultistsLeader);
        }


        foreach (var selectedCultist in selectedCultists)
        {
            if (_actorSystem.TryGetActorFromUserId(selectedCultist.Data.UserId, out _, out var ent))
            {
                component.CultistsNames.Add(MetaData(ent!.Value).EntityName);
            }
        }

        if (cultistsLeader != null)
        {
            MakeCultistLeader(cultistsLeader);
            if (_actorSystem.TryGetActorFromUserId(cultistsLeader.Data.UserId, out _, out var ent))
            {
                component.CultistsNames.Add(MetaData(ent!.Value).EntityName);
            }
        }

        foreach (var cultist in selectedCultists)
        {
            MakeCultist(cultist);
        }

        component.SelectionStatus = FleshCultRuleComponent.SelectionState.SelectionMade;
    }

    private void OnPlayersSpawned(RulePlayerJobsAssignedEvent ev)
    {
        var query = EntityQueryEnumerator<FleshCultRuleComponent, GameRuleComponent>();
        while (query.MoveNext(out var uid, out var fleshCult, out var gameRule))
        {

            if (!GameTicker.IsGameRuleAdded(uid, gameRule))
                continue;
            foreach (var player in ev.Players)
            {
                if (!ev.Profiles.ContainsKey(player.UserId))
                    continue;

                fleshCult.StartCandidates[player] = ev.Profiles[player.UserId];
            }

            DoCultistStart(fleshCult);

            fleshCult.SelectionStatus = FleshCultRuleComponent.SelectionState.ReadyToSelect;
        }
    }

    public List<IPlayerSession> FindPotentialCultists(in Dictionary<IPlayerSession,
        HumanoidCharacterProfile> candidates, FleshCultRuleComponent component)
    {
        var list = new List<IPlayerSession>();
        var pendingQuery = GetEntityQuery<PendingClockInComponent>();

        foreach (var player in candidates.Keys)
        {
            var mindId = player.Data.ContentData()?.Mind;
            if (mindId == null || !TryComp<MindComponent>(mindId, out var mind))
            {
                continue;
            }

            if (_roleSystem.MindIsAntagonist(mindId.Value))
            {
                continue;
            }

            // Role prevents antag.
            if (!_jobs.CanBeAntag(player))
            {
                continue;
            }

            if (TryComp<HumanoidAppearanceComponent>(mind.OwnedEntity, out var appearanceComponent))
            {
                if (!component.SpeciesWhitelist.Contains(appearanceComponent.Species))
                    continue;
            }

            // Latejoin
            if (player.AttachedEntity != null && pendingQuery.HasComponent(player.AttachedEntity.Value))
                continue;

            list.Add(player);
        }

        var prefList = new List<IPlayerSession>();

        foreach (var player in list)
        {
            var profile = candidates[player];
            if (profile.AntagPreferences.Contains(component.FleshCultistPrototypeId))
            {
                prefList.Add(player);
            }
        }
        if (prefList.Count == 0)
        {
            _sawmill.Info("Insufficient preferred traitors, picking at random.");
            prefList = list;
        }
        return prefList;
    }

    public List<IPlayerSession> FindPotentialCultistsLeader(in Dictionary<IPlayerSession,
        HumanoidCharacterProfile> candidates, FleshCultRuleComponent component)
    {
        var list = new List<IPlayerSession>();
        var pendingQuery = GetEntityQuery<PendingClockInComponent>();

        foreach (var player in candidates.Keys)
        {
            var mindId = player.Data.ContentData()?.Mind;
            if (mindId == null || !TryComp<MindComponent>(mindId, out var mind))
            {
                continue;
            }

            if (_roleSystem.MindIsAntagonist(mindId.Value))
            {
                continue;
            }

            if (TryComp<HumanoidAppearanceComponent>(mind.OwnedEntity, out var appearanceComponent))
            {
                if (!component.SpeciesWhitelist.Contains(appearanceComponent.Species))
                    continue;
            }

            // Latejoin
            if (player.AttachedEntity != null && pendingQuery.HasComponent(player.AttachedEntity.Value))
                continue;

            list.Add(player);
        }

        var prefList = new List<IPlayerSession>();

        foreach (var player in list)
        {
            var profile = candidates[player];
            if (profile.AntagPreferences.Contains(component.FleshCultistLeaderPrototypeId))
            {
                prefList.Add(player);
            }
        }
        if (prefList.Count == 0)
        {
            _sawmill.Info("Insufficient preferred traitors, picking at random.");
            prefList = list;
        }
        return prefList;
    }

    public List<IPlayerSession> PickCultists(int cultistCount, List<IPlayerSession> prefList)
    {
        cultistCount = Math.Max(0, cultistCount);
        var results = new List<IPlayerSession>(cultistCount);
        if (prefList.Count == 0 || cultistCount == 0)
        {
            _sawmill.Info("Insufficient ready players to fill up with traitors, stopping the selection.");
            return results;
        }

        for (var i = 0; i < cultistCount; i++)
        {
            if (prefList.Count == 0)
            {
                break;
            }
            results.Add(_random.PickAndTake(prefList));
            _sawmill.Info("Selected a preferred traitor.");
        }
        return results;
    }

    [ValidatePrototypeId<RadioChannelPrototype>]
    public const string FleshChannel = "Flesh";

    [ValidatePrototypeId<EntityPrototype>]
    public const string CreateFleshHeartObjective = "CreateFleshHeartObjective";

    [ValidatePrototypeId<EntityPrototype>]
    public const string FleshCultistSurvivalObjective = "FleshCultistSurvivalObjective";

    private bool BaseMakeCultist(IPlayerSession traitor, FleshCultRuleComponent fleshCultRule, EntityUid mindId, MindComponent mind, string role)
    {
        if (mind.OwnedEntity is not { } entity)
        {
            _sawmill.Error("Mind picked for traitor did not have an attached entity.");
            return false;
        }

        DebugTools.AssertNotNull(mind.OwnedEntity);

        _roleSystem.MindAddRole(mindId, new FleshCultistRoleComponent
        {
            PrototypeId = role
        });
        fleshCultRule.Cultists.Add((mindId, mind));

        _faction.RemoveFaction(entity, "NanoTrasen", false);
        _faction.AddFaction(entity, "Flesh");

        var storeComp = EnsureComp<StoreComponent>(mind.OwnedEntity.Value);

        EnsureComp<IntrinsicRadioReceiverComponent>(mind.OwnedEntity.Value);
        var radio = EnsureComp<ActiveRadioComponent>(mind.OwnedEntity.Value);
        radio.Channels.Add(FleshChannel);
        var transmitter = EnsureComp<IntrinsicRadioTransmitterComponent>(mind.OwnedEntity.Value);
        transmitter.Channels.Add(FleshChannel);

        storeComp.Categories.Add("FleshCultistAbilities");
        storeComp.CurrencyWhitelist.Add("StolenMutationPoint");
        storeComp.BuySuccessSound = fleshCultRule.BuySuccesSound;

        EnsureComp<FleshCultistComponent>(mind.OwnedEntity.Value);

        if (_prototypeManager.TryIndex<RadioChannelPrototype>(FleshChannel, out var fleshChannel))
        {
            var hiveMind = EnsureComp<PsionicComponent>(mind.OwnedEntity.Value);
            hiveMind.Channel = FleshChannel;
            hiveMind.Removable = false;
            hiveMind.ChannelColor = fleshChannel.Color;
        }

        _mindSystem.TryAddObjective(mindId, mind, CreateFleshHeartObjective);
        _mindSystem.TryAddObjective(mindId, mind, FleshCultistSurvivalObjective);

        // Assign briefing
        _roleSystem.MindAddRole(mindId, new RoleBriefingComponent
        {
            Briefing = Loc.GetString("flesh-cult-role-cult-members",
                ("cultMembers", string.Join(", ", fleshCultRule.CultistsNames)))
        });

        _audioSystem.PlayGlobal(fleshCultRule.AddedSound, Filter.SinglePlayer(traitor), false, AudioParams.Default);
        return true;
    }

    public bool MakeCultist(IPlayerSession traitor)
    {
        var fleshCultRule = EntityQuery<FleshCultRuleComponent>().FirstOrDefault();
        if (fleshCultRule == null)
        {
            //todo fuck me this shit is awful
            GameTicker.StartGameRule("FleshCult", out var ruleEntity);
            fleshCultRule = EntityManager.GetComponent<FleshCultRuleComponent>(ruleEntity);
        }

        var mindId = traitor.Data.ContentData()?.Mind;
        if (mindId == null || !TryComp<MindComponent>(mindId, out var mind))
        {
            _sawmill.Info("Failed getting mind for picked cultist.");
            return false;
        }

        return BaseMakeCultist(traitor, fleshCultRule, mindId.Value, mind, fleshCultRule.FleshCultistPrototypeId);
    }

    public bool MakeCultistLeader(IPlayerSession traitor)
    {
        var fleshCultRule = EntityQuery<FleshCultRuleComponent>().FirstOrDefault();
        if (fleshCultRule == null)
        {
            //todo fuck me this shit is awful
            GameTicker.StartGameRule("FleshCult", out var ruleEntity);
            fleshCultRule = EntityManager.GetComponent<FleshCultRuleComponent>(ruleEntity);
        }

        var mindId = traitor.Data.ContentData()?.Mind;
        if (mindId == null || !TryComp<MindComponent>(mindId, out var mind))
        {
            _sawmill.Info("Failed getting mind for picked cultist.");
            return false;
        }

        return BaseMakeCultist(traitor, fleshCultRule, mindId.Value, mind, fleshCultRule.FleshCultistLeaderPrototypeId);
    }

    private void HandleLatejoin(PlayerSpawnCompleteEvent ev)
    {
        var query = EntityQueryEnumerator<FleshCultRuleComponent, GameRuleComponent>();
        while (query.MoveNext(out var uid, out var fleshCult, out var gameRule))
        {
            if (!GameTicker.IsGameRuleAdded(uid, gameRule))
                continue;
            if (fleshCult.TotalCultists >= MaxCultists)
                return;
            if (!ev.LateJoin)
                return;
            if (!ev.Profile.AntagPreferences.Contains(fleshCult.FleshCultistPrototypeId))
                return;


            if (ev.JobId == null || !_prototypeManager.TryIndex<JobPrototype>(ev.JobId, out var job))
                return;

            if (!job.CanBeAntag)
                return;

            // Before the announcement is made, late-joiners are considered the same as players who readied.
            if (fleshCult.SelectionStatus < FleshCultRuleComponent.SelectionState.SelectionMade)
            {
                fleshCult.StartCandidates[ev.Player] = ev.Profile;
                return;
            }

            // the nth player we adjust our probabilities around
            int target = ((PlayersPerCultist * fleshCult.TotalCultists) + 1);

            float chance = (1f / PlayersPerCultist);

            // If we have too many traitors, divide by how many players below target for next traitor we are.
            if (ev.JoinOrder < target)
            {
                chance /= (target - ev.JoinOrder);
            }
            else // Tick up towards 100% chance.
            {
                chance *= ((ev.JoinOrder + 1) - target);
            }

            if (chance > 1)
                chance = 1;

            // Now that we've calculated our chance, roll and make them a traitor if we roll under.
            // You get one shot.
            if (_random.Prob(chance))
            {
                MakeCultist(ev.Player);
            }
        }
    }

    private void OnRoundEndText(RoundEndTextAppendEvent ev)
    {
        var query = EntityQueryEnumerator<FleshCultRuleComponent, GameRuleComponent>();
        while (query.MoveNext(out var uid, out var fleshCult, out var gameRule))
        {
            if (!GameTicker.IsGameRuleAdded(uid, gameRule))
                continue;

            var result = Loc.GetString("flesh-cult-round-end-result", ("cultistsCount",
                fleshCult.Cultists.Count));

            if (fleshCult.WinType is FleshCultRuleComponent.WinTypes.FleshHeartFinal)
            {
                result += "\n" + Loc.GetString("flesh-cult-round-end-flesh-heart-succes");
            }
            else
            {
                result += "\n" + Loc.GetString("flesh-cult-round-end-flesh-heart-fail");
            }

            // result += "\n" + Loc.GetString("traitor-round-end-codewords", ("codewords", string.Join(", ", Codewords))) + "\n";

            foreach (var (mindId, mind) in fleshCult.Cultists)
            {
                var name = mind.CharacterName;
                _mindSystem.TryGetSession(mind, out var session);
                var username = session?.Name;

                var objectives = mind.AllObjectives.ToArray();

                var leader = "";
                if (TryComp<FleshCultistRoleComponent>(mindId, out var cultist) && cultist.PrototypeId == fleshCult.FleshCultistLeaderPrototypeId)
                {
                    leader = "-leader";
                }

                if (objectives.Length == 0)
                {
                    if (username != null)
                    {
                        if (name == null)
                        {
                            result += "\n" + Loc.GetString($"flesh-cult-user-was-a-cultist{leader}",
                                ("user", username));
                        }
                        else
                        {
                            result += "\n" + Loc.GetString($"flesh-cult-user-was-a-cultist{leader}-named",
                                ("user", username), ("name", name));
                        }
                    }
                    else if (name != null)
                        result += "\n" + Loc.GetString($"flesh-cult-was-a-cultist{leader}-named", ("name", name));

                    continue;
                }

                if (username != null)
                {
                    if (name == null)
                    {
                        result += "\n" + Loc.GetString($"flesh-cult-user-was-a-cultist{leader}-with-objectives",
                            ("user", username));
                    }
                    else
                    {
                        result += "\n" + Loc.GetString($"flesh-cult-user-was-a-cultist{leader}-with-objectives-named",
                            ("user", username), ("name", name));
                    }
                }
                else if (name != null)
                {
                    result += "\n" + Loc.GetString($"flesh-cult-was-a-cultist{leader}-with-objectives-named",
                        ("name", name));
                }

                foreach (var objectiveGroup in objectives.GroupBy(o => Comp<ObjectiveComponent>(o).Issuer))
                {
                    if (objectiveGroup.Key == "Космический банк")
                    {
                        continue;
                    }

                    result += "\n" + Loc.GetString($"preset-flesh-cult-objective-issuer-{objectiveGroup.Key}");

                    foreach (var objective in objectiveGroup)
                    {
                        var info = _objectivesSystem.GetInfo(objective, mindId);
                        if (info == null)
                            continue;

                        var objectiveTitle = info.Value.Title;
                        var progress = info.Value.Progress;
                        if (progress > 0.99f)
                        {
                            result += "\n- " + Loc.GetString(
                                "flesh-cult-objective-condition-success",
                                ("condition", objectiveTitle),
                                ("markupColor", "green")
                            );
                        }
                        else
                        {
                            result += "\n- " + Loc.GetString(
                                "flesh-cult-objective-condition-fail",
                                ("condition", objectiveTitle),
                                ("progress", (int) (progress * 100)),
                                ("markupColor", "red")
                            );
                        }

                    }
                }
            }
            result += "\n" +
                      "\n";

            ev.AddLine(result);
        }
    }
}
