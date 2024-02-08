using System.Linq;
using Content.Server.Antag;
using Content.Server.Chat.Managers;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Rules;
using Content.Server.GameTicking.Rules.Components;
using Content.Server.Objectives;
using Content.Shared.Backmen.CCVar;
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

    public override void Initialize()
    {
        base.Initialize();

        _sawmill = Logger.GetSawmill("preset");

        SubscribeLocalEvent<RoundStartAttemptEvent>(OnStartAttempt);
        SubscribeLocalEvent<RulePlayerJobsAssignedEvent>(OnPlayersSpawned);
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(HandleLatejoin);
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
