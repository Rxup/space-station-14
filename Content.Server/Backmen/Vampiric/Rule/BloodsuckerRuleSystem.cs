using System.Linq;
using Content.Server.Antag;
using Content.Server.Backmen.Vampiric.Role;
using Content.Server.Bible.Components;
using Content.Server.Chat.Managers;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Rules;
using Content.Server.GameTicking.Rules.Components;
using Content.Server.Mind;
using Content.Server.Objectives;
using Content.Shared.Antag;
using Content.Shared.Backmen.CCVar;
using Content.Shared.Backmen.Vampiric;
using Content.Shared.Humanoid;
using Content.Shared.Mind;
using Content.Shared.Mobs.Components;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Robust.Server.Placement;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

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
    [Dependency] private readonly GameTicker _gameTicker = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;

    public override void Initialize()
    {
        base.Initialize();

        _sawmill = Logger.GetSawmill("preset");

        SubscribeLocalEvent<RoundStartAttemptEvent>(OnStartAttempt);
        SubscribeLocalEvent<RulePlayerJobsAssignedEvent>(OnPlayersSpawned);
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(HandleLatejoin);
    }

    [ValidatePrototypeId<EntityPrototype>]
    private const string VampireObjective = "VampireObjective";

    protected override void Added(EntityUid uid, BloodsuckerRuleComponent component, GameRuleComponent gameRule, GameRuleAddedEvent args)
    {
        base.Added(uid, component, gameRule, args);

        if (_gameTicker.RunLevel == GameRunLevel.InRound)
        {
            _gameTicker.StartGameRule(VampireObjective);
        }
        else
        {
            _gameTicker.AddGameRule(VampireObjective);
        }
    }

    private void HandleLatejoin(PlayerSpawnCompleteEvent ev)
    {
        var query = QueryActiveRules();
        while (query.MoveNext(out _, out var vpmRule, out _))
        {
            if (vpmRule.TotalBloodsuckers >= MaxBloodsuckers)
                continue;

            if (!ev.LateJoin)
                continue;

            var whitelistSpecies = vpmRule.SpeciesWhitelist;
            if (!_antagSelection.IsPlayerEligible(ev.Player, Bloodsucker, acceptableAntags: AntagAcceptability.NotExclusive,
                    allowNonHumanoids: false,
                    customExcludeCondition: ent =>
                    {
                        if (HasComp<BibleUserComponent>(ent))
                        {
                            return true;
                        }

                        return TryComp<HumanoidAppearanceComponent>(ent, out var humanoidAppearanceComponent) &&
                               !whitelistSpecies.Contains(humanoidAppearanceComponent.Species.Id);
                    }))
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
                _antagSelection.SendBriefing(ev.Player, Loc.GetString("vampire-role-greeting"), Color.Plum, vpmRule.InitialInfectedSound);
            }
        }
    }

    private void OnPlayersSpawned(RulePlayerJobsAssignedEvent ev)
    {
        var query = QueryActiveRules();
        while (query.MoveNext(out var uid, out _, out var comp, out var gameRule))
        {
            //Get all players eligible for this role, allow selecting existing antags
            //TO DO: When voxes specifies are added, increase their chance of becoming a thief by 4 times >:)
            var whitelistSpecies = comp.SpeciesWhitelist;
            var eligiblePlayers = _antagSelection.GetEligiblePlayers(
                ev.Players, Bloodsucker,
                acceptableAntags: AntagAcceptability.NotExclusive,
                allowNonHumanoids: false,
                customExcludeCondition: ent =>
                {
                    if (HasComp<BibleUserComponent>(ent))
                    {
                        return true;
                    }

                    return TryComp<HumanoidAppearanceComponent>(ent, out var humanoidAppearanceComponent) &&
                           !whitelistSpecies.Contains(humanoidAppearanceComponent.Species.Id);
                });

            //Abort if there are none
            if (eligiblePlayers.Count == 0)
            {
                //Log.Warning($"No eligible thieves found, ending game rule {ToPrettyString(uid):rule}");
                //GameTicker.EndGameRule(uid, gameRule);
                continue;
            }

            var initialInfectedCount = _antagSelection.CalculateAntagCount(_playerManager.PlayerCount, PlayersPerBloodsucker, MaxBloodsuckers);

            //Select our theives
            var thieves = _antagSelection.ChooseAntags(initialInfectedCount, eligiblePlayers);

            DoVampirStart(thieves, comp);

            _antagSelection.SendBriefing(thieves, Loc.GetString("vampire-role-greeting"), Color.Plum, comp.InitialInfectedSound);
        }
    }

    [ValidatePrototypeId<AntagPrototype>]
    private const string Bloodsucker = "Bloodsucker";

    private void DoVampirStart(List<EntityUid> startCandidates, BloodsuckerRuleComponent vpmRule)
    {
        foreach (var traitor in startCandidates)
        {
            _bloodSuckerSystem.ConvertToVampire(traitor);
            vpmRule.TotalBloodsuckers++;
            if (_mindSystem.TryGetMind(traitor, out var mindId, out _))
            {
                vpmRule.Elders.Add(MetaData(traitor).EntityName,mindId);
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
