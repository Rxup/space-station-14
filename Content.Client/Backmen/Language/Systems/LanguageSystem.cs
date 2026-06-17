using Content.Shared.Backmen.Language;
using Content.Shared.Backmen.Language.Events;
using Content.Shared.Backmen.Language.Systems;
using Robust.Client;
using Robust.Client.Player;
using Robust.Shared.GameStates;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Client.Backmen.Language.Systems;

/// <summary>
///   Client-side language system.
/// </summary>
public sealed partial class LanguageSystem : SharedLanguageSystem
{
    public ProtoId<LanguagePrototype> CurrentLanguage => GetCurrentLanguage()?.CurrentLanguage ?? default;
    public HashSet<ProtoId<LanguagePrototype>> SpokenLanguages => GetCurrentLanguage()?.SpokenLanguages ?? [];
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

    private void OnLanguagesUpdated(Entity<LanguageSpeakerComponent> ent)
    {
        if (ent.Owner != _playerManager.LocalEntity)
            return;

        OnLanguagesChanged?.Invoke(this, ent);
    }

    public void RequestSetLanguage(LanguagePrototype language)
    {
        if (language.ID == CurrentLanguage)
            return;

        if (GetCurrentLanguage() is not { } comp)
            return;

        if (!comp.SpokenLanguages.Contains(language.ID))
            return;

        RaiseNetworkEvent(new LanguagesSetMessage(language.ID));

        if (comp.CurrentLanguage == language.ID)
            return;

        comp.CurrentLanguage = language.ID;
        if (_playerManager.LocalEntity is { } local)
            OnLanguagesUpdated((local, comp));
    }
}
