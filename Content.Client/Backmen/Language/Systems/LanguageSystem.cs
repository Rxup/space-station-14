using Content.Shared.Backmen.Language;
using Content.Shared.Backmen.Language.Events;
using Content.Shared.Backmen.Language.Systems;
using Robust.Client;
using Robust.Client.Player;
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
    public ProtoId<LanguagePrototype> CurrentLanguage => GetCurrentLanguage()?.CurrentLanguage ?? default;
    /// <summary>
    ///   The list of languages the currently possessed entity can speak.
    /// </summary>
    public HashSet<ProtoId<LanguagePrototype>> SpokenLanguages => GetCurrentLanguage()?.SpokenLanguages ?? [];
    /// <summary>
    ///   The list of languages the currently possessed entity can understand.
    /// </summary>
    public HashSet<ProtoId<LanguagePrototype>> UnderstoodLanguages => GetCurrentLanguage()?.UnderstoodLanguages ?? [];

    public event EventHandler<Entity<LanguageSpeakerComponent>>? OnLanguagesChanged;

    [Dependency] private IPlayerManager _playerManager = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<LanguageSpeakerComponent, LocalPlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<LanguageSpeakerComponent, LocalPlayerDetachedEvent>(OnPlayerDetached);
        SubscribeLocalEvent<LanguageSpeakerComponent, AfterAutoHandleStateEvent>(OnLanguagesState);
    }

    private LanguageSpeakerComponent? GetCurrentLanguage()
    {
        return CompOrNull<LanguageSpeakerComponent>(_playerManager.LocalEntity);
    }

    private void OnLanguagesState(Entity<LanguageSpeakerComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        OnLanguagesUpdated(ent);
    }

    private void OnPlayerDetached(Entity<LanguageSpeakerComponent> ent, ref LocalPlayerDetachedEvent args)
    {
        OnLanguagesUpdated(ent);
    }

    private void OnPlayerAttached(Entity<LanguageSpeakerComponent> ent, ref LocalPlayerAttachedEvent args)
    {
        OnLanguagesUpdated(ent);
    }

    private void OnLanguagesUpdated(Entity<LanguageSpeakerComponent> message)
    {
        OnLanguagesChanged?.Invoke(this, message);
    }

    public void RequestSetLanguage(LanguagePrototype language)
    {
        if (language.ID == CurrentLanguage)
            return;

        RaiseNetworkEvent(new LanguagesSetMessage(language.ID));

        // May cause some minor desync...
        // So to reduce the probability of desync, we replicate the change locally too
        if (GetCurrentLanguage() is {} cur && cur.SpokenLanguages.Contains(language.ID))
            cur.CurrentLanguage = language.ID;
    }
}
