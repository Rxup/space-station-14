using Content.Shared.Backmen.Language;
using Content.Shared.Backmen.Language.Events;
using Content.Shared.Backmen.Language.Systems;
using Robust.Client;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Client.Backmen.Language.Systems;

/// <summary>
///   Client-side language system.
/// </summary>
/// <remarks>
///   Unlike the server, the client is not aware of other entities' languages; it's only notified about the entity that it posesses.
///   Due to that, this system stores such information in a static manner.
/// </remarks>
public sealed partial class LanguageSystem : SharedLanguageSystem
{
    /// <summary>
    ///   The current language of the entity currently possessed by the player.
    /// </summary>
    public ProtoId<LanguagePrototype> CurrentLanguage { get; private set; } = default!;
    /// <summary>
    ///   The list of languages the currently possessed entity can speak.
    /// </summary>
    public List<ProtoId<LanguagePrototype>> SpokenLanguages { get; private set; } = new();
    /// <summary>
    ///   The list of languages the currently possessed entity can understand.
    /// </summary>
    public List<ProtoId<LanguagePrototype>> UnderstoodLanguages { get; private set; } = new();

    public event EventHandler<Entity<LanguageSpeakerComponent>>? OnLanguagesChanged;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<LanguageSpeakerComponent, LocalPlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<LanguageSpeakerComponent, LocalPlayerDetachedEvent>(OnPlayerDetached);
        SubscribeLocalEvent<LanguageSpeakerComponent, AfterAutoHandleStateEvent>(OnLanguagesState);
    }

    private void OnLanguagesState(Entity<LanguageSpeakerComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        OnLanguagesUpdated(ent);
    }

    private void OnPlayerDetached(Entity<LanguageSpeakerComponent> ent, ref LocalPlayerDetachedEvent args)
    {
        CurrentLanguage = default!;
        SpokenLanguages = [];
        UnderstoodLanguages = [];
    }

    private void OnPlayerAttached(Entity<LanguageSpeakerComponent> ent, ref LocalPlayerAttachedEvent args)
    {
        OnLanguagesUpdated(ent);
    }

    private void OnLanguagesUpdated(Entity<LanguageSpeakerComponent> message)
    {
        // TODO this entire thing is horrible. If someone is willing to refactor this, LanguageSpeakerComponent should become shared with SendOnlyToOwner = true
        // That way, this system will be able to use the existing networking infrastructure instead of relying on this makeshift... whatever this is.
        CurrentLanguage = message.Comp.CurrentLanguage ?? default!;
        SpokenLanguages = message.Comp.SpokenLanguages;
        UnderstoodLanguages = message.Comp.UnderstoodLanguages;

        OnLanguagesChanged?.Invoke(this, message);
    }

    public void RequestSetLanguage(LanguagePrototype language)
    {
        if (language.ID == CurrentLanguage)
            return;

        RaiseNetworkEvent(new LanguagesSetMessage(language.ID));

        // May cause some minor desync...
        // So to reduce the probability of desync, we replicate the change locally too
        if (SpokenLanguages.Contains(language.ID))
            CurrentLanguage = language.ID;
    }
}
