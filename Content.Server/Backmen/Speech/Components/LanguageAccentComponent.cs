using Content.Shared.Backmen.Language;
using Robust.Shared.Prototypes;

namespace Content.Server.Backmen.Speech.Components;

[RegisterComponent]
public sealed partial class LanguageAccentComponent : Component
{
    [DataField(required: true), ViewVariables(VVAccess.ReadWrite)]
    public ProtoId<LanguagePrototype> Language { get; set; }

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float Chance { get; set; } = 0.2f;
}
