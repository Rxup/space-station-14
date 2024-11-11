using Robust.Shared.Serialization;

namespace Content.Shared.Backmen.Teams.CapturePoint;

[Serializable, NetSerializable]
public sealed class BkmCapturePointChangeEvent(Dictionary<StationTeamMarker, int> captureInfo) : EntityEventArgs
{
    public readonly Dictionary<StationTeamMarker, int> CaptureInfo = captureInfo;
}

public sealed class BkmCaptureChangeStatusEvent(StationTeamMarker team) : EntityEventArgs
{
    public readonly StationTeamMarker Team = team;
}

public sealed class BkmCaptureDoneEvent(StationTeamMarker team) : EntityEventArgs
{
    public readonly StationTeamMarker Team = team;
}

[Serializable, NetSerializable]
public enum BkmCPTVisualState : byte
{
    TeamA,
    TeamAToNeutral,
    TeamB,
    TeamBToNeutral,

    TeamNeutral,
    TeamNeutralToA,
    TeamNeutralToB,
}
