using System.Linq;
using Content.Server.Antag;
using Content.Server.Backmen.Vampiric.Role;
using Content.Server.Chat.Managers;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Rules;
using Content.Server.GameTicking.Rules.Components;
using Content.Server.Mind;
using Content.Server.Objectives;
using Content.Shared.Backmen.CCVar;
using Content.Shared.Backmen.Vampiric;
using Content.Shared.Mind;
using Content.Shared.Mobs.Components;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Robust.Shared.Configuration;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.Backmen.Vampiric;

public sealed class BloodsuckerRuleSystem : GameRuleSystem<BloodsuckerRuleComponent>
{

    private ISawmill _sawmill = default!;

    private int PlayersPerBloodsucker => _cfg.GetCVar(CCVars.BloodsuckerPlayersPerBloodsucker);
    private int MaxBloodsuckers => _cfg.GetCVar(CCVars.BloodsuckerMaxPerBloodsucker);

    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly IChatManager _chatManager = default!;
    [Dependency] private readonly AntagSelectionSystem _antagSelection = default!;
    [Dependency] private readonly BloodSuckerSystem _bloodSuckerSystem = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly MindSystem _mindSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        _sawmill = Logger.GetSawmill("preset");

        SubscribeLocalEvent<RoundStartAttemptEvent>(OnStartAttempt);
        SubscribeLocalEvent<RulePlayerJobsAssignedEvent>(OnPlayersSpawned);
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(HandleLatejoin);
        SubscribeLocalEvent<RoundEndTextAppendEvent>(OnRoundEndText);
    }

    private void OnRoundEndText(RoundEndTextAppendEvent ev)
    {
        var query = AllEntityQuery<BloodsuckerRuleComponent>();
        while (query.MoveNext(out var vampRule))
        {
            ev.AddLine(Loc.GetString("vampire-elder"));

            foreach (var player in vampRule.Elders)
            {
                var role = CompOrNull<VampireRoleComponent>(player.Value);
                var count = role?.Converted ?? 0;
                var blood = role?.Drink ?? 0;
                var countGoal = 0;
                var bloodGoal = 0f;

                var mind = CompOrNull<MindComponent>(player.Value);
                if (_mindSystem.TryGetObjectiveComp<BloodsuckerDrinkConditionComponent>(player.Value, out var obj1, mind))
                {
                    bloodGoal = obj1.Goal;
                }
                if (_mindSystem.TryGetObjectiveComp<BloodsuckerConvertConditionComponent>(player.Value, out var obj2, mind))
                {
                    countGoal = obj2.Goal;
                }

                _mindSystem.TryGetSession(player.Value, out var session);
                var username = session?.Name;
                if (username != null)
                {
                    ev.AddLine(Loc.GetString("endgame-vamp-name-user", ("name", player.Key), ("username", username)));
                }
                else
                {
                    ev.AddLine(Loc.GetString("endgame-vamp-name", ("name", player.Key)));
                }
                ev.AddLine(Loc.GetString("endgame-vamp-conv",
                    ("count", count), ("goal", countGoal)));
                ev.AddLine(Loc.GetString("endgame-vamp-drink",
                    ("count", blood), ("goal", bloodGoal)));
            }

            ev.AddLine("");
            ev.AddLine(Loc.GetString("vampire-bitten"));
            var q = EntityQueryEnumerator<MindComponent,VampireRoleComponent>();
            while (q.MoveNext(out var mindId,out var mind, out var role))
            {
                if (vampRule.Elders.ContainsValue(mindId))
                {
                    continue;
                }
                var count = role?.Converted ?? 0;
                var blood = role?.Drink ?? 0;
                var countGoal = 0;
                var bloodGoal = 0f;

                if (_mindSystem.TryGetObjectiveComp<BloodsuckerDrinkConditionComponent>(mindId, out var obj1,mind))
                {
                    bloodGoal = obj1.Goal;
                }
                if (_mindSystem.TryGetObjectiveComp<BloodsuckerConvertConditionComponent>(mindId, out var obj2,mind))
                {
                    countGoal = obj2.Goal;
                }

                _mindSystem.TryGetSession(mindId, out var session);
                var username = session?.Name;
                if (username != null)
                {
                    ev.AddLine(Loc.GetString("endgame-vamp-name-user", ("name", mind.CharacterName ?? "-"), ("username", username)));
                }
                else
                {
                    ev.AddLine(Loc.GetString("endgame-vamp-name", ("name", mind.CharacterName ?? "-")));
                }
                ev.AddLine(Loc.GetString("endgame-vamp-conv",
                    ("count", count), ("goal", countGoal)));
                ev.AddLine(Loc.GetString("endgame-vamp-drink",
                    ("count", blood), ("goal", bloodGoal)));
            }
        }
    }


    private void HandleLatejoin(PlayerSpawnCompleteEvent ev)
    {
        var query = EntityQueryEnumerator<BloodsuckerRuleComponent, GameRuleComponent>();
        while (query.MoveNext(out var uid, out var vpmRule, out var gameRule))
        {
            if (!GameTicker.IsGameRuleAdded(uid, gameRule))
                continue;

            if (vpmRule.TotalBloodsuckers >= MaxBloodsuckers)
                continue;
            if (!ev.LateJoin)
                continue;
            if (!ev.Profile.AntagPreferences.Contains("Bloodsucker"))
                continue;

            if (ev.JobId == null || !_prototypeManager.TryIndex<JobPrototype>(ev.JobId, out var job))
                continue;

            if (!job.CanBeAntag)
                continue;

            if(!vpmRule.SpeciesWhitelist.Contains(ev.Profile.Species))
                continue;

            // the nth player we adjust our probabilities around
            var target = PlayersPerBloodsucker * vpmRule.TotalBloodsuckers + 1;

            var chance = 1f / PlayersPerBloodsucker;

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
            if (_random.Prob(chance) && ev.Player.AttachedEntity.HasValue)
            {
                _bloodSuckerSystem.ConvertToVampire(ev.Player.AttachedEntity.Value);
                vpmRule.TotalBloodsuckers++;
                if (_mindSystem.TryGetMind(ev.Player, out var mindId, out _))
                {
                    vpmRule.Elders.Add(MetaData(ev.Player.AttachedEntity.Value).EntityName,mindId);
                }
            }
        }
    }

    private void OnPlayersSpawned(RulePlayerJobsAssignedEvent ev)
    {
        var query = EntityQueryEnumerator<BloodsuckerRuleComponent, GameRuleComponent>();
        while (query.MoveNext(out var uid, out var vpmRule, out var gameRule))
        {
            var plr = new Dictionary<ICommonSession, HumanoidCharacterProfile>();

            if (!GameTicker.IsGameRuleAdded(uid, gameRule))
                continue;

            foreach (var player in ev.Players)
            {
                if (!ev.Profiles.ContainsKey(player.UserId))
                    continue;

                if(!vpmRule.SpeciesWhitelist.Contains(ev.Profiles[player.UserId].Species))
                    continue;

                plr.Add(player, ev.Profiles[player.UserId]);
            }

            DoVampirStart(vpmRule, plr);
        }
    }

    private void DoVampirStart(BloodsuckerRuleComponent vpmRule, Dictionary<ICommonSession, HumanoidCharacterProfile> startCandidates)
    {
        var numTraitors = MathHelper.Clamp(startCandidates.Count / PlayersPerBloodsucker, 1, MaxBloodsuckers);
        var traitorPool = _antagSelection.FindPotentialAntags(startCandidates, "Bloodsucker");
        var selectedTraitors = _antagSelection.PickAntag(numTraitors, traitorPool);

        foreach (var traitor in selectedTraitors)
        {
            if (traitor.AttachedEntity.HasValue)
            {
                _bloodSuckerSystem.ConvertToVampire(traitor.AttachedEntity.Value);
                vpmRule.TotalBloodsuckers++;
                if (_mindSystem.TryGetMind(traitor, out var mindId, out _))
                {
                    vpmRule.Elders.Add(MetaData(traitor.AttachedEntity.Value).EntityName,mindId);
                }
            }
        }
    }


    private void OnStartAttempt(RoundStartAttemptEvent ev)
    {
        var query = EntityQueryEnumerator<BloodsuckerRuleComponent, GameRuleComponent>();
        while (query.MoveNext(out var uid, out _, out var gameRule))
        {
            if (!GameTicker.IsGameRuleAdded(uid, gameRule))
                continue;

            if (ev.Players.Length == 0)
            {
                _chatManager.DispatchServerAnnouncement(Loc.GetString("bloodsucker-no-one-ready"));
                ev.Cancel();
            }
        }
    }
}
