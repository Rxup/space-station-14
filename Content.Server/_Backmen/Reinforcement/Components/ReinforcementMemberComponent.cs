using Content.Shared._Backmen.Reinforcement.Components;

namespace Content.Server._Backmen.Reinforcement.Components;

[RegisterComponent]
public sealed partial class ReinforcementMemberComponent : Component
{
    [ViewVariables(VVAccess.ReadOnly)]
    public Entity<ReinforcementConsoleComponent> Linked;
}
