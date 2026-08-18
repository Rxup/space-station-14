using Content.Server.DoAfter;
using Content.Server.Popups;
using Content.Shared.DoAfter;
using Content.Shared.Backmen.Language;
using Content.Shared.Backmen.Language.Events;
using Content.Shared.Backmen.Language.Components;
using Content.Shared.Examine;
using Content.Shared.Interaction.Events;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;

namespace Content.Server.Backmen.Language;

public sealed partial class LanguageLearnSystem : EntitySystem
{
    [Dependency] private DoAfterSystem _doAfterSystem = default!;
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private LanguageSystem _language = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<LanguageLearnComponent, UseInHandEvent>(OnUseInHand);
        SubscribeLocalEvent<LanguageLearnComponent, LanguageLearnDoAfterEvent>(OnUsed, after: new[] { typeof(DoAfterSystem) });
        SubscribeLocalEvent<LanguageLearnComponent, ExaminedEvent>(OnExamine);
    }

    private void OnUseInHand(EntityUid uid, LanguageLearnComponent component, UseInHandEvent args)
    {
        if (args.Handled)
            return;

        if (component.GetUsesRemaining() <= 0)
        {
            _popup.PopupEntity(Loc.GetString("language-item-no-uses"), uid, args.User);
            return;
        }

        if (KnowsAllLanguages(args.User, component))
        {
            _popup.PopupEntity(Loc.GetString("language-item-already-knows"), uid, args.User);
            args.Handled = true;
            return;
        }

        args.Handled = true;

        var ev = new LanguageLearnDoAfterEvent();
        var doAfter = new DoAfterArgs(EntityManager, args.User, component.DoAfterDuration, ev, uid)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true
        };

        _doAfterSystem.TryStartDoAfter(doAfter);
        _audio.PlayPvs(component.UseSound, uid);
    }

    private void OnUsed(EntityUid uid, LanguageLearnComponent component, LanguageLearnDoAfterEvent args)
    {
        if (args.Cancelled)
            return;

        var learnedSomething = false;

        foreach (var language in component.Languages)
        {
            if (KnowsSpokenLanguage(args.User, language))
                continue;

            _language.AddLanguage(args.User, language);
            learnedSomething = true;
        }

        if (!learnedSomething)
        {
            _popup.PopupEntity(Loc.GetString("language-item-already-knows"), uid, args.User);
            return;
        }

        _audio.PlayPvs(component.UseSound, uid);

        var usesRemaining = component.GetUsesRemaining() - 1;
        component.UsesRemaining = usesRemaining;
        DirtyField(uid, component, nameof(LanguageLearnComponent.UsesRemaining));

        if (component.DeleteAfterUse && usesRemaining <= 0)
            QueueDel(uid);
    }

    private void OnExamine(EntityUid uid, LanguageLearnComponent component, ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        args.PushMarkup(Loc.GetString("language-item-uses-remaining", ("uses", component.GetUsesRemaining())));
    }

    private bool KnowsAllLanguages(EntityUid user, LanguageLearnComponent component)
    {
        if (component.Languages.Count == 0)
            return true;

        foreach (var language in component.Languages)
        {
            if (!KnowsSpokenLanguage(user, language))
                return false;
        }

        return true;
    }

    private bool KnowsSpokenLanguage(EntityUid user, ProtoId<LanguagePrototype> language)
    {
        return TryComp<LanguageKnowledgeComponent>(user, out var knowledge)
               && knowledge.SpokenLanguages.Contains(language);
    }
}
