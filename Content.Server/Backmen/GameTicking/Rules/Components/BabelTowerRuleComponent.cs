using Content.Shared.Backmen.Language;
using Robust.Shared.Prototypes;

namespace Content.Server.Backmen.GameTicking.Rules.Components;

[RegisterComponent]
public sealed partial class BabelTowerRuleComponent : Component
{
    [ViewVariables(VVAccess.ReadOnly)]
    public List<ProtoId<LanguagePrototype>> LanguagesToRemove =
    [
        "TauCetiBasic",
    ];
}
