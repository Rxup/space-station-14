namespace Content.Shared._Backmen.Magic.Events;

public sealed class CanUseMagicEvent : CancellableEntityEventArgs
{
    public EntityUid User { get; set; }
}
