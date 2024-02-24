using Robust.Shared.Prototypes;

namespace Content.Shared.Backmen.Reinforcement.Components;

[RegisterComponent]
[Access(typeof(SharedReinforcementSystem), Other = AccessPermissions.ReadExecute)]
public sealed partial class ReinforcementConsoleComponent : Component
{
    public int MaxStringLength { get; set; } = 256;

    /// <summary>
    /// Entity, id of member in current console, metadata - name of entity
    /// </summary>
    public List<ReinforcementRowRecord> Members = new();
    public string Brief = "";
    public EntityUid CalledBy = EntityUid.Invalid;

    public bool IsActive = false;

    [DataField("available")]
    public List<ProtoId<ReinforcementPrototype>> Available = new();

    public ProtoId<ReinforcementPrototype>? GetById(int id)
    {
        return Available.Count <= id ? null! : Available[id];
    }
    public ProtoId<ReinforcementPrototype>? GetById(uint id)
    {
        return GetById((int) id);
    }
    public ReinforcementPrototype? GetById(uint id, IPrototypeManager prototypeManager)
    {
        var row = GetById(id);
        if (row == null || !prototypeManager.TryIndex(row.Value, out var proto))
        {
            return null;
        }

        return proto;
    }
}

public sealed class ReinforcementRowRecord
{
    public EntityUid Owner { get; set; } = EntityUid.Invalid;
    public uint Id { get; set; }
    public string Name { get; set; } = "";
}
