using Content.Shared.Backmen.Language;
using Content.Shared.Radio;
using Robust.Shared.Prototypes;

namespace Content.Server.Backmen.Blob.Components;

[RegisterComponent]
public sealed partial class BlobSpeakComponent : Component
{
    [DataField]
    public ProtoId<LanguagePrototype> Language = "Blob";

    /// <summary>
    /// Hide entity name
    /// </summary>
    [DataField]
    public bool OverrideName = true;

    [DataField]
    public LocId Name = "speak-vv-blob";

    /// <summary>
    ///     The list of all languages the entity may speak.
    ///     By default, contains the languages this entity speaks intrinsically.
    /// </summary>
    public HashSet<ProtoId<LanguagePrototype>> OldSpokenLanguages = new();

    /// <summary>
    ///     The list of all languages the entity may understand.
    ///     By default, contains the languages this entity understands intrinsically.
    /// </summary>
    public HashSet<ProtoId<LanguagePrototype>> OldUnderstoodLanguages = new();
}
