namespace Content.Shared.Backmen.Reinforcement.Components;

[RegisterComponent]
[Access(typeof(SharedReinforcementSystem))]
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
}

public sealed class ReinforcementRowRecord
{
    public EntityUid Owner { get; set; } = EntityUid.Invalid;
    public uint Id { get; set; }
    public string Name { get; set; } = "";
}
