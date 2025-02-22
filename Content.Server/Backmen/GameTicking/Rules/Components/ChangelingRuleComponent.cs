using Content.Shared.Store;
using Robust.Shared.Prototypes;

namespace Content.Server.Backmen.GameTicking.Rules.Components;

[RegisterComponent, Access(typeof(ChangelingRuleSystem))]
public sealed partial class ChangelingRuleComponent : Component
{
    public readonly List<EntityUid> ChangelingMinds = [];

    public readonly List<ProtoId<StoreCategoryPrototype>> StoreCategories =
    [
        "ChangelingAbilityCombat",
        "ChangelingAbilitySting",
        "ChangelingAbilityUtility",
    ];

    public readonly List<EntProtoId> Objectives =
    [
        "ChangelingSurviveObjective",
        "ChangelingStealDNAObjective",
        "EscapeIdentityObjective",
    ];
}
