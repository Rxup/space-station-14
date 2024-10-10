using Content.Server.Backmen.GameTicking.Rules.Components;
using Content.Server.Backmen.Language;
using Content.Server.Backmen.Language.Events;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Rules;
using Content.Shared.Backmen.Language;
using Content.Shared.GameTicking;
using Content.Shared.GameTicking.Components;

namespace Content.Server.Backmen.GameTicking.Rules;

public sealed class BabelTowerRuleSystem : GameRuleSystem<BabelTowerRuleComponent>
{
    [Dependency] private readonly LanguageSystem _language = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DetermineEntityLanguagesEvent>(OnLanguageApply, after: [typeof(TranslatorSystem), typeof(TranslatorImplantSystem)]);
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawned);
        SubscribeLocalEvent<RoundStartedEvent>(OnRoundStarted);
    }

    private void OnRoundStarted(RoundStartedEvent ev)
    {
        var queue = QueryActiveRules();
        while (queue.MoveNext(out _, out _, out _))
        {
            var q2 = EntityQueryEnumerator<LanguageSpeakerComponent>();
            while (q2.MoveNext(out var owner, out var comp))
            {
                _language.UpdateEntityLanguages((owner, comp));
            }
            break;
        }
    }

    protected override void Started(EntityUid uid, BabelTowerRuleComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        base.Started(uid, component, gameRule, args);

        UpdateAllPlayers();
    }

    protected override void Ended(EntityUid uid, BabelTowerRuleComponent component, GameRuleComponent gameRule, GameRuleEndedEvent args)
    {
        base.Ended(uid, component, gameRule, args);

        UpdateAllPlayers();
    }

    private void UpdateAllPlayers()
    {
        var queue = EntityQueryEnumerator<LanguageSpeakerComponent>();
        while (queue.MoveNext(out var owner, out var comp))
        {
            _language.UpdateEntityLanguages((owner, comp));
        }
    }

    private void OnPlayerSpawned(PlayerSpawnCompleteEvent ev)
    {
        var queue = QueryActiveRules();
        while (queue.MoveNext(out _, out _, out _))
        {
            _language.UpdateEntityLanguages(ev.Mob);
            break;
        }
    }

    private void OnLanguageApply(ref DetermineEntityLanguagesEvent ev)
    {
        var queue = QueryActiveRules();
        while (queue.MoveNext(out _, out var lungComp, out _))
        {
            ev.SpokenLanguages.RemoveWhere(x => lungComp.LanguagesToRemove.Contains(x));
            ev.UnderstoodLanguages.RemoveWhere(x => lungComp.LanguagesToRemove.Contains(x));
            break;
        }
    }
}
