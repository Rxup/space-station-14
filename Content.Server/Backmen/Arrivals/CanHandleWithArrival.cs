using Content.Server.Station.Systems;

namespace Content.Server.Backmen.Arrivals;

public sealed class CanHandleWithArrival(PlayerSpawningEvent player) : CancellableEntityEventArgs
{
    public PlayerSpawningEvent Player = player;
}
