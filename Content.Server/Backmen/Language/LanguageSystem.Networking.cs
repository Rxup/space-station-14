using Content.Shared.Backmen.Language.Events;

namespace Content.Server.Backmen.Language;

public sealed partial class LanguageSystem
{
    private void InitializeNet()
    {
        SubscribeNetworkEvent<LanguagesSetMessage>(OnClientSetLanguage);
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
}
