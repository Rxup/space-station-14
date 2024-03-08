using System.Linq;
using Content.Server.Antag;
using Content.Server.Backmen.Vampiric;
using Content.Server.Bible.Components;
using Content.Server.Chat.Managers;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Rules;
using Content.Server.GameTicking.Rules.Components;
using Content.Server.Mind;
using Content.Shared.Actions;
using Content.Shared.Antag;
using Content.Shared.Backmen.Blob;
using Content.Shared.Backmen.Blob.Components;
using Content.Shared.Backmen.CCVar;
using Content.Shared.Humanoid;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.Backmen.Blob.Rule;

public sealed class BlobGameRuleSystem : GameRuleSystem<BlobGameRuleComponent>
{
    private ISawmill _sawmill = default!;


    private int PlayersPerBlob => _cfg.GetCVar(CCVars.BlobPlayersPer);
    private int MaxBlob => _cfg.GetCVar(CCVars.BlobMax);

    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly IChatManager _chatManager = default!;
    [Dependency] private readonly AntagSelectionSystem _antagSelection = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;


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
        var query = QueryActiveRules();
        while (query.MoveNext(out _, out var blob, out var gameRule))
        {
            if (blob.TotalBlobs >= MaxBlob)
                continue;

            if (!ev.LateJoin)
                continue;

            if (!_antagSelection.IsPlayerEligible(ev.Player, Blob, acceptableAntags: AntagAcceptability.NotExclusive, allowNonHumanoids: false))
                continue;

            // the nth player we adjust our probabilities around
            var target = PlayersPerBlob * blob.TotalBlobs + 1;

            var chance = 1f / PlayersPerBlob;

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
                MakeBlob(blob, ev.Player.AttachedEntity.Value);
                _antagSelection.SendBriefing(ev.Player, Loc.GetString("blob-carrier-role-greeting"), Color.Plum, blob.InitialInfectedSound);
            }
        }
    }

    private void MakeBlob(BlobGameRuleComponent blob, EntityUid player)
    {
        blob.TotalBlobs++;
        var comp = EnsureComp<BlobCarrierComponent>(player);
        comp.HasMind = HasComp<ActorComponent>(player);
        comp.TransformationDelay = 10 * 60; // 10min
        _actions.SetCooldown(comp.TransformToBlob, TimeSpan.FromMinutes(5));
    }

    private void OnPlayersSpawned(RulePlayerJobsAssignedEvent ev)
    {
        var query = QueryActiveRules();
        while (query.MoveNext(out var uid, out var blob, out var gameRule))
        {
            var eligiblePlayers = _antagSelection.GetEligiblePlayers(
                ev.Players, Blob,
                acceptableAntags: AntagAcceptability.None,
                allowNonHumanoids: false, includeAllJobs: false);

            if (eligiblePlayers.Count == 0)
            {
                //Log.Warning($"No eligible thieves found, ending game rule {ToPrettyString(uid):rule}");
                //GameTicker.EndGameRule(uid, gameRule);
                continue;
            }

            var initialInfectedCount = _antagSelection.CalculateAntagCount(_playerManager.PlayerCount, PlayersPerBlob, MaxBlob);

            var blobs = _antagSelection.ChooseAntags(initialInfectedCount, eligiblePlayers);

            DoBlobStart(blobs, blob);

            _antagSelection.SendBriefing(blobs, Loc.GetString("blob-carrier-role-greeting"), Color.Plum, blob.InitialInfectedSound);
        }
    }

    private void DoBlobStart(List<EntityUid> selectedTraitors, BlobGameRuleComponent blob)
    {
        foreach (var traitor in selectedTraitors)
        {
            MakeBlob(blob, traitor);
        }
    }

    private void OnStartAttempt(RoundStartAttemptEvent ev)
    {
        var query = EntityQueryEnumerator<BlobGameRuleComponent, GameRuleComponent>();
        while (query.MoveNext(out var uid, out _, out var gameRule))
        {
            if (!GameTicker.IsGameRuleAdded(uid, gameRule))
                continue;

            if (ev.Players.Length == 0)
            {
                _chatManager.DispatchServerAnnouncement(Loc.GetString("blob-no-one-ready"));
                ev.Cancel();
            }
        }
    }

    [ValidatePrototypeId<AntagPrototype>]
    private const string Blob = "Blob";
}
