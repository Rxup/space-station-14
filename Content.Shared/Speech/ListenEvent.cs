using Content.Shared.Backmen.Language;

namespace Content.Shared.Speech;

public sealed class ListenEvent : EntityEventArgs
{
    public readonly LanguagePrototype? Language; // backmen: language
    public readonly string Message;
    // start-backmen: npc-listen-accentless
    /// <summary>
    /// Speech before accent transformations. Same as <see cref="Message"/> when no accent was applied.
    /// </summary>
    public readonly string OriginalMessage;
    // end-backmen: npc-listen-accentless
    public readonly EntityUid Source;

    public ListenEvent(string message, EntityUid source, LanguagePrototype? language = null, string? originalMessage = null) // backmen: npc-listen-accentless
    {
        Language = language;
        Message = message;
        OriginalMessage = originalMessage ?? message; // backmen: npc-listen-accentless
        Source = source;
    }
}

public sealed class ListenAttemptEvent : CancellableEntityEventArgs
{
    public readonly EntityUid Source;

    public ListenAttemptEvent(EntityUid source)
    {
        Source = source;
    }
}
