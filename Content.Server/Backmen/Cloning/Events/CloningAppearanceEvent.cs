using Content.Server.Backmen.Cloning.Components;
using Robust.Shared.Map;
using Robust.Shared.Player;

namespace Content.Server.Backmen.Cloning.Events;

public sealed class CloningAppearanceEvent : EntityEventArgs
{
    public ICommonSession Player = default!;
    public CloningAppearanceComponent Component = default!;
    public EntityCoordinates Coords { get; set; }
    public EntityUid? StationUid { get; set; }
}
