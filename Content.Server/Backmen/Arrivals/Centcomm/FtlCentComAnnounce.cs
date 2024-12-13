using Content.Server.Shuttles.Components;

namespace Content.Server.Backmen.Arrivals.CentComm;

public sealed class FtlCentComAnnounce : EntityEventArgs
{
    public Entity<ShuttleComponent> Source { get; set; }
}
