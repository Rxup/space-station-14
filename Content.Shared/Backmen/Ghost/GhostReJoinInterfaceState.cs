using Content.Shared.Eui;
using Robust.Shared.Serialization;

namespace Content.Shared.Backmen.Ghost;

[Serializable,NetSerializable]
public sealed record GhostReJoinStation(NetEntity Station, string Name);

[Serializable,NetSerializable]
public sealed record GhostReJoinCharacter(int Id, string Name);

[Serializable, NetSerializable]
public sealed class GhostReJoinInterfaceState : EuiStateBase
{
    public List<GhostReJoinStation> Stations { get; } = new();
    public List<GhostReJoinCharacter> Characters { get; } = new();

}

[Serializable, NetSerializable]
public sealed class GhostReJoinCharacterMessage : EuiMessageBase
{
    public GhostReJoinCharacterMessage(int id, NetEntity station)
    {
        Id = id;
        Station = station;
    }

    public int Id { get; }
    public NetEntity Station { get; }
}

[Serializable, NetSerializable]
public sealed class GhostReJoinRandomMessage : EuiMessageBase
{
    public GhostReJoinRandomMessage(NetEntity station)
    {
        Station = station;
    }

    public NetEntity Station { get; }
}
