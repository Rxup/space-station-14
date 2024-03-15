using Content.Shared.Mobs;
using Robust.Shared.Serialization;

namespace Content.Shared.Backmen.Reinforcement;

public abstract class SharedReinforcementSystem : EntitySystem
{

}

[Serializable, NetSerializable]
public enum ReinforcementConsoleKey : byte
{
    Key
}

[Serializable, NetSerializable]
public sealed class BriefReinforcementUpdate : BoundUserInterfaceMessage
{
    public readonly string? Brief;

    public BriefReinforcementUpdate(string? brief)
    {
        Brief = brief;
    }
}

[Serializable, NetSerializable]
public sealed class ChangeReinforcementMsg : BoundUserInterfaceMessage
{
    public readonly uint? Id;
    public readonly uint Count = 0;

    public ChangeReinforcementMsg(uint id, uint count)
    {
        Id = id;
        Count = count;
    }
}

[Serializable, NetSerializable]
public sealed class CallReinforcementStart : BoundUserInterfaceMessage
{

}


[Serializable, NetSerializable]
public sealed class UpdateReinforcementUi : BoundUserInterfaceState
{
    public bool IsActive = false;
    public string CalledBy = "";
    public string Brief = "";
    public List<(uint id, NetEntity owner, string name, MobState status)> Members = new();

}
