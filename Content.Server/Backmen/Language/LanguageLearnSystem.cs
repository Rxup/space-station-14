using Content.Server.DoAfter;
using Content.Server.Popups;
using Content.Shared.DoAfter;
using Content.Shared.Backmen.Language.Events;
using Content.Shared.Backmen.Language.Components;
using Content.Shared.Examine;
using Content.Shared.Interaction.Events;
using Robust.Shared.Audio.Systems;

namespace Content.Server.Backmen.Language;

public sealed class LanguageLearnSystem : EntitySystem
{
    [Dependency] private readonly DoAfterSystem _doAfterSystem = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly LanguageSystem _language = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<LanguageLearnComponent, UseInHandEvent>(OnUseInHand);
        SubscribeLocalEvent<LanguageLearnComponent, LanguageLearnDoAfterEvent>(OnLanguageItemUsed, after: new []{typeof(DoAfterSystem)});
        SubscribeLocalEvent<LanguageLearnComponent, ExaminedEvent>(OnExamine);
    }

    private void OnUseInHand(EntityUid uid, LanguageLearnComponent component, UseInHandEvent args)
    {
        if (args.Handled)
            return;

        if (component.UsesRemaining <= 0)
        {
            _popup.PopupEntity(Loc.GetString("language-item-no-uses"), uid, args.User);
            return;
        }

        args.Handled = true;

        var Event = new LanguageLearnDoAfterEvent();
        var Args = new DoAfterArgs(EntityManager, args.User, component.DoAfterDuration, Event, uid)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true
        };

        _doAfterSystem.TryStartDoAfter(Args);
        _audio.PlayPvs(component.UseSound, uid);
    }

    private void OnLanguageItemUsed(EntityUid uid, LanguageLearnComponent component, LanguageLearnDoAfterEvent args)
    {
        if (args.Cancelled)
            return;

        if (!TryComp<LanguageKnowledgeComponent>(args.User, out var languageKnowledge))
            return;

        if (languageKnowledge.SpokenLanguages.Contains(component.Language))
        {
            _popup.PopupEntity(Loc.GetString("language-item-already-knows", ("language", component.Language)), uid, args.User);
            return;
        }

        languageKnowledge.SpokenLanguages.Add(component.Language);
        languageKnowledge.UnderstoodLanguages.Add(component.Language);
        _language.UpdateEntityLanguages(args.User);
        _audio.PlayPvs(component.UseSound, uid);

        component.UsesRemaining--;
        Dirty(uid, component);

        if (component.DeleteAfterUse && component.UsesRemaining <= 0)
        {
            EntityManager.QueueDeleteEntity(uid);
        }
    }

    private void OnExamine(EntityUid uid, LanguageLearnComponent component, ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        args.PushMarkup(Loc.GetString("language-item-uses-remaining", ("uses", component.UsesRemaining)));
    }
}
