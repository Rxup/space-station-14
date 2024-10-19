using Content.Shared.Backmen.Language;
using Robust.Shared.Prototypes;

namespace Content.Server.Backmen.Language;

/// <summary>
///     Raised in order to determine the list of languages the entity can speak and understand at the given moment.
///     Typically raised on an entity after a language agent (e.g. a translator) has been added to or removed from them.
/// </summary>
[ByRefEvent]
public record struct DetermineEntityLanguagesEvent
{
    public EntityUid EntityUid { get; init; }

    /// <summary>
    ///     The list of all languages the entity may speak.
    ///     By default, contains the languages this entity speaks intrinsically.
    /// </summary>
    public HashSet<ProtoId<LanguagePrototype>> SpokenLanguages = new();

    /// <summary>
    ///     The list of all languages the entity may understand.
    ///     By default, contains the languages this entity understands intrinsically.
    /// </summary>
    public HashSet<ProtoId<LanguagePrototype>> UnderstoodLanguages = new();

    public DetermineEntityLanguagesEvent() { }
}
