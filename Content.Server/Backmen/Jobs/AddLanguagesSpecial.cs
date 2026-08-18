using Content.Server.Backmen.Language;
using Content.Shared.Backmen.Language;
using Content.Shared.Roles;
using JetBrains.Annotations;
using Robust.Shared.Prototypes;

namespace Content.Server.Backmen.Jobs;

/// <summary>
/// Adds intrinsic spoken and understood languages on spawn without replacing existing knowledge.
/// </summary>
[UsedImplicitly]
public sealed partial class AddLanguagesSpecial : JobSpecial
{
    [DataField]
    public List<ProtoId<LanguagePrototype>> Speaks = new();

    [DataField]
    public List<ProtoId<LanguagePrototype>> Understands = new();

    public override void AfterEquip(EntityUid mob)
    {
        var entMan = IoCManager.Resolve<IEntityManager>();
        var language = entMan.System<LanguageSystem>();

        foreach (var lang in Speaks)
            language.AddLanguage(mob, lang);

        foreach (var lang in Understands)
            language.AddLanguage(mob, lang, addSpoken: false);
    }
}
