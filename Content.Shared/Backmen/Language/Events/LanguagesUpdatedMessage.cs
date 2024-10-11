using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Backmen.Language.Events;

/// <summary>
///     Sent to the client when its list of languages changes.
///     The client should in turn update its HUD and relevant systems.
/// </summary>
[Serializable, NetSerializable]
public sealed class LanguagesUpdatedMessage(ProtoId<LanguagePrototype> currentLanguage, List<ProtoId<LanguagePrototype>> spoken, List<ProtoId<LanguagePrototype>> understood) : EntityEventArgs
{
    public ProtoId<LanguagePrototype> CurrentLanguage = currentLanguage;
    public List<ProtoId<LanguagePrototype>> Spoken = spoken;
    public List<ProtoId<LanguagePrototype>> Understood = understood;
}
