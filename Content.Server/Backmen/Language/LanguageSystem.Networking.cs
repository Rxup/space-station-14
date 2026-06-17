using Content.Shared.Backmen.Language;
using Content.Shared.Backmen.Language.Events;
using Robust.Shared.Player;

namespace Content.Server.Backmen.Language;

public sealed partial class LanguageSystem
{
    private void InitializeNet()
    {
        SubscribeNetworkEvent<LanguagesSetMessage>(OnClientSetLanguage);
        SubscribeLocalEvent<LanguageSpeakerComponent, PlayerAttachedEvent>(OnNetSync);
    }

    private void OnClientSetLanguage(LanguagesSetMessage message, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { Valid: true } uid)
            return;

        var language = GetLanguagePrototype(message.CurrentLanguage);
        if (language == null || !CanSpeak(uid, language.ID))
            return;

        SetLanguage(uid, language.ID);
    }

    private void OnNetSync(Entity<LanguageSpeakerComponent> ent, ref PlayerAttachedEvent args)
    {
        DirtyFields(ent, ent.Comp, null,
            nameof(LanguageSpeakerComponent.CurrentLanguage),
            nameof(LanguageSpeakerComponent.SpokenLanguages),
            nameof(LanguageSpeakerComponent.UnderstoodLanguages));
    }
}
