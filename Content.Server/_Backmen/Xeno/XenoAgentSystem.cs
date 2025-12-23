using Content.Server._Backmen.Language;
using Content.Server._Backmen.Language.Events;
using Content.Server._Backmen.Xeno.Components;
using Content.Shared._Backmen.Language;

namespace Content.Server._Backmen.Xeno;

public sealed class XenoAgentSystem : EntitySystem
{
    [Dependency] private readonly LanguageSystem _language = default!;
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<XenoAgentComponent, MapInitEvent>(OnInit);
        SubscribeLocalEvent<XenoAgentComponent, DetermineEntityLanguagesEvent>(OnApplyLanguages);
    }

    [ValidatePrototypeId<LanguagePrototype>]
    private const string XenoLanguage = "Xeno";

    private void OnApplyLanguages(Entity<XenoAgentComponent> ent, ref DetermineEntityLanguagesEvent args)
    {
        args.SpokenLanguages.Add(XenoLanguage);
        args.UnderstoodLanguages.Add(XenoLanguage);
    }

    private void OnInit(Entity<XenoAgentComponent> ent, ref MapInitEvent args)
    {
        _language.UpdateEntityLanguages(ent.Owner);
    }
}
