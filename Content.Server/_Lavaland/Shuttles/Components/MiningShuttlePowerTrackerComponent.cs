namespace Content.Server._Lavaland.Shuttles.Components;

/// <summary>
/// Tracks which powered entities existed on the mining shuttle at mapinit.
/// </summary>
[RegisterComponent]
public sealed partial class MiningShuttlePowerTrackerComponent : Component
{
    [DataField]
    public HashSet<EntityUid> MapInitReceivers = new();
}
