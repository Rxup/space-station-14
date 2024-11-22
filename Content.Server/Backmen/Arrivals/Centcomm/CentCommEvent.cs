namespace Content.Server.Backmen.Arrivals.CentComm;

public sealed class CentCommEvent(EntityUid station,CentComEventId eventId) : HandledEntityEventArgs
{
    public EntityUid Station { get; } = station;
    public CentComEventId EventId { get; } = eventId;
}
