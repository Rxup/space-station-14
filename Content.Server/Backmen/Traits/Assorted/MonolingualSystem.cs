using Content.Server.Backmen.Language;

namespace Content.Server.Backmen.Traits.Assorted;

public sealed class MonolingualSystem : EntitySystem
{

    [Dependency] private readonly LanguageSystem _language = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<MonolingualComponent, ComponentInit>(OnInit);
    }

    private void OnInit(EntityUid uid, MonolingualComponent component, ComponentInit args)
    {
        _language.RemoveLanguage(uid, "TauCetiBasic");
    }
}