using Content.Shared.Backmen.Reinforcement.Components;

namespace Content.Server.Backmen.Reinforcement.Components;

[RegisterComponent]
public sealed partial class ReinforcementMindComponent : Component
{
    [ViewVariables(VVAccess.ReadOnly)]
    public Entity<ReinforcementConsoleComponent> Linked;
}
