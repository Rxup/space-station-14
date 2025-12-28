namespace Content.Shared._Backmen.Disease.Events;

public sealed class AttemptSneezeCoughEvent(string? EmoteId) : CancellableEntityEventArgs
{
    public string? EmoteId { get; } = EmoteId;
}
