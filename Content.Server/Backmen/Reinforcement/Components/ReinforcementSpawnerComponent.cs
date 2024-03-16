using Content.Shared.Backmen.Reinforcement.Components;

namespace Content.Server.Backmen.Reinforcement.Components;

[RegisterComponent]
public sealed partial class ReinforcementSpawnerComponent : Component
{
    [ViewVariables(VVAccess.ReadOnly)]
    public Entity<ReinforcementConsoleComponent> Linked;
    [ViewVariables(VVAccess.ReadWrite)]
    public bool Used = false;
}
