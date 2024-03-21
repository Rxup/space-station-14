using Content.Server.Station.Systems;
using Robust.Shared.Player;

namespace Content.Server.Backmen.Arrivals;

public sealed class CanHandleWithArrival(PlayerSpawningEvent player) : CancellableEntityEventArgs
{
    public PlayerSpawningEvent Player = player;
}
