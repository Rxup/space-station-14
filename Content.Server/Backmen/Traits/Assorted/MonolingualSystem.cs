using Content.Server.Backmen.Language;

namespace Content.Server.Backmen.Traits.Assorted;

public sealed class MonolingualSystem : EntitySystem
{

    [Dependency] private readonly LanguageSystem _language = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<MonolingualComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<MonolingualComponent, ComponentRemove>(OnRemove);

        SubscribeLocalEvent<MonolingualComponent, DetermineEntityLanguagesEvent>(OnLanguageApply);
    }

    private void OnInit(EntityUid uid, MonolingualComponent component, ComponentInit args)
    {
        _language.UpdateEntityLanguages(uid);
    }

    private void OnRemove(EntityUid uid, MonolingualComponent component, ComponentRemove args)
	{
        _language.UpdateEntityLanguages(uid);
    }

    private void OnLanguageApply(EntityUid uid, MonolingualComponent component, DetermineEntityLanguagesEvent ev)
    {
        if(component.LifeStage is
           ComponentLifeStage.Removing
           or ComponentLifeStage.Stopping
           or ComponentLifeStage.Stopped)
            return;

        ev.SpokenLanguages.Remove("TauCetiBasic");
        ev.UnderstoodLanguages.Remove("TauCetiBasic");
    }
}
