using System.Linq;
using Content.Server.Backmen.Language.Events;
using Content.Shared.Backmen.Language;
using Content.Shared.Backmen.Language.Components;
using Content.Shared.Backmen.Language.Systems;
using Robust.Shared.Prototypes;
using UniversalLanguageSpeakerComponent = Content.Shared.Backmen.Language.Components.UniversalLanguageSpeakerComponent;

namespace Content.Server.Backmen.Language;

public sealed partial class LanguageSystem : SharedLanguageSystem
{
    private EntityQuery<LanguageSpeakerComponent> _languageSpeakerQuery;
    private EntityQuery<UniversalLanguageSpeakerComponent> _universalLanguageSpeakerQuery;

    public override void Initialize()
    {
        base.Initialize();
        InitializeNet();

        SubscribeLocalEvent<LanguageSpeakerComponent, ComponentInit>(OnInitLanguageSpeaker);
        SubscribeLocalEvent<UniversalLanguageSpeakerComponent, MapInitEvent>(OnUniversalInit);
        SubscribeLocalEvent<UniversalLanguageSpeakerComponent, ComponentShutdown>(OnUniversalShutdown);

        _languageSpeakerQuery = GetEntityQuery<LanguageSpeakerComponent>();
        _universalLanguageSpeakerQuery = GetEntityQuery<UniversalLanguageSpeakerComponent>();
    }

    private void OnUniversalShutdown(EntityUid uid, UniversalLanguageSpeakerComponent component, ComponentShutdown args)
    {
        RemoveLanguage(uid, UniversalPrototype);
    }

    private void OnUniversalInit(EntityUid uid, UniversalLanguageSpeakerComponent component, MapInitEvent args)
    {
        AddLanguage(uid, UniversalPrototype);
    }

    #region public api

    public bool CanUnderstand(Entity<LanguageSpeakerComponent?> listener, string language)
    {
        if (language == UniversalPrototype || _universalLanguageSpeakerQuery.HasComp(listener))
            return true;

        if (!_languageSpeakerQuery.Resolve(listener, ref listener.Comp, logMissing: false))
            return false;

        return listener.Comp.UnderstoodLanguages.Contains(language);
    }

    public bool CanSpeak(Entity<LanguageSpeakerComponent?> speaker, string language)
    {
        if (_universalLanguageSpeakerQuery.HasComp(speaker))
            return true;

        if (!_languageSpeakerQuery.Resolve(speaker, ref speaker.Comp, logMissing: false))
            return false;

        return speaker.Comp.SpokenLanguages.Contains(language);
    }

    /// <summary>
    ///     Returns the current language of the given entity, assumes Universal if it's not a language speaker.
    /// </summary>
    public LanguagePrototype GetLanguage(Entity<LanguageSpeakerComponent?> speaker)
    {
        if (!_languageSpeakerQuery.Resolve(speaker, ref speaker.Comp, logMissing: false)
            || string.IsNullOrEmpty(speaker.Comp.CurrentLanguage)
            || !_prototype.TryIndex(speaker.Comp.CurrentLanguage, out var proto))
            return Universal;

        return proto;
    }

    /// <summary>
    ///     Returns the list of languages this entity can speak.
    /// </summary>
    /// <remarks>Typically, checking <see cref="LanguageSpeakerComponent.SpokenLanguages"/> is sufficient.</remarks>
    public List<ProtoId<LanguagePrototype>> GetSpokenLanguages(Entity<LanguageSpeakerComponent?> uid)
    {
        if (!_languageSpeakerQuery.Resolve(uid, ref uid.Comp, logMissing: false))
            return [];

        return uid.Comp.SpokenLanguages;
    }

    /// <summary>
    ///     Returns the list of languages this entity can understand.
    /// </summary>
    /// <remarks>Typically, checking <see cref="LanguageSpeakerComponent.UnderstoodLanguages"/> is sufficient.</remarks>
    public List<ProtoId<LanguagePrototype>> GetUnderstoodLanguages(Entity<LanguageSpeakerComponent?> uid)
    {
        if (!_languageSpeakerQuery.Resolve(uid, ref uid.Comp, logMissing: false))
            return [];

        return uid.Comp.UnderstoodLanguages;
    }

    public void SetLanguage(EntityUid speaker, string language, LanguageSpeakerComponent? component = null)
    {
        if (!CanSpeak(speaker, language)
            || !Resolve(speaker, ref component)
            || component.CurrentLanguage == language)
            return;

        component.CurrentLanguage = language;
        RaiseLocalEvent(speaker, new LanguagesUpdateEvent(), true);
    }

    /// <summary>
    ///     Adds a new language to the respective lists of intrinsically known languages of the given entity.
    /// </summary>
    public void AddLanguage(
        Entity<LanguageKnowledgeComponent?> uid,
        string language,
        bool addSpoken = true,
        bool addUnderstood = true)
    {
        EnsureComp<LanguageKnowledgeComponent>(uid, out uid.Comp);
        var speaker = EnsureComp<LanguageSpeakerComponent>(uid);

        if (addSpoken && !uid.Comp.SpokenLanguages.Contains(language))
            uid.Comp.SpokenLanguages.Add(language);

        if (addUnderstood && !uid.Comp.UnderstoodLanguages.Contains(language))
            uid.Comp.UnderstoodLanguages.Add(language);

        UpdateEntityLanguages((uid, speaker));
    }

    /// <summary>
    ///     Removes a language from the respective lists of intrinsically known languages of the given entity.
    /// </summary>
    public void RemoveLanguage(
        Entity<LanguageKnowledgeComponent?> uid,
        string language,
        bool removeSpoken = true,
        bool removeUnderstood = true)
    {
        if (!Resolve(uid, ref uid.Comp, false))
            return;

        if (removeSpoken)
            uid.Comp.SpokenLanguages.Remove(language);

        if (removeUnderstood)
            uid.Comp.UnderstoodLanguages.Remove(language);

        UpdateEntityLanguages(uid.Owner);
    }

    /// <summary>
    ///   Ensures the given entity has a valid language as its current language.
    ///   If not, sets it to the first entry of its SpokenLanguages list, or universal if it's empty.
    /// </summary>
    /// <returns>True if the current language was modified, false otherwise.</returns>
    public bool EnsureValidLanguage(Entity<LanguageSpeakerComponent?> entity)
    {
        if (!Resolve(entity, ref entity.Comp))
            return false;

        if (!entity.Comp.SpokenLanguages.Contains(entity.Comp.CurrentLanguage ?? ""))
        {
            entity.Comp.CurrentLanguage = entity.Comp.SpokenLanguages.FirstOrDefault(UniversalPrototype);
            RaiseLocalEvent(entity, new LanguagesUpdateEvent());
            return true;
        }

        return false;
    }

    /// <summary>
    ///     Immediately refreshes the cached lists of spoken and understood languages for the given entity.
    /// </summary>
    public void UpdateEntityLanguages(Entity<LanguageSpeakerComponent?> entity)
    {
        if (!_languageSpeakerQuery.Resolve(entity, ref entity.Comp, logMissing: false))
            return;

        Log.Debug($"{ToPrettyString(entity.Owner)} UpdateEntityLanguages");

        var ev = new DetermineEntityLanguagesEvent
        {
            EntityUid = entity
        };
        // We add the intrinsically known languages first so other systems can manipulate them easily
        if (TryComp<LanguageKnowledgeComponent>(entity, out var knowledge))
        {
            foreach (var spoken in knowledge.SpokenLanguages)
            {
                ev.SpokenLanguages.Add(spoken);
            }

            foreach (var understood in knowledge.UnderstoodLanguages)
            {
                ev.UnderstoodLanguages.Add(understood);
            }
        }

        RaiseLocalEvent(entity, ref ev, false);
        RaiseLocalEvent(ref ev);

        entity.Comp.SpokenLanguages.Clear();
        entity.Comp.UnderstoodLanguages.Clear();

        entity.Comp.SpokenLanguages.AddRange(ev.SpokenLanguages);
        entity.Comp.UnderstoodLanguages.AddRange(ev.UnderstoodLanguages);

        if (!EnsureValidLanguage(entity))
            RaiseLocalEvent(entity, new LanguagesUpdateEvent());
    }

    #endregion

    #region event handling

    private void OnInitLanguageSpeaker(EntityUid uid, LanguageSpeakerComponent component, ComponentInit args)
    {
        if (string.IsNullOrEmpty(component.CurrentLanguage))
            component.CurrentLanguage = component.SpokenLanguages.FirstOrDefault(UniversalPrototype);

        UpdateEntityLanguages(uid);
    }

    #endregion
}
