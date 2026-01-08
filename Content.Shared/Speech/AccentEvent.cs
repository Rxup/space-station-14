using Content.Shared.Backmen.Language;
using Robust.Shared.Prototypes;

namespace Content.Shared.Speech;

public sealed class AccentGetEvent : EntityEventArgs
{
    /// <summary>
    ///     The entity to apply the accent to.
    /// </summary>
    public EntityUid Entity { get; }

    /// <summary>
    ///     The message to apply the accent transformation to.
    ///     Modify this to apply the accent.
    /// </summary>
    public string Message { get; set; }

    public ProtoId<LanguagePrototype>? LanguageOverride { get; set; } // backmen

    public AccentGetEvent(EntityUid entity, string message)
    {
        Entity = entity;
        Message = message;
    }
}
