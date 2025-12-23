using Content.Shared._Backmen.Reinforcement.Components;

namespace Content.Server._Backmen.Reinforcement.Components;

[RegisterComponent]
public sealed partial class ReinforcementSpawnerComponent : Component
{
    [ViewVariables(VVAccess.ReadOnly)]
    public Entity<ReinforcementConsoleComponent> Linked;
    [ViewVariables(VVAccess.ReadWrite)]
    public bool Used = false;
}
