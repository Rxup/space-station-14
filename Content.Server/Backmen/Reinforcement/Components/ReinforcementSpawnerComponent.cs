using Content.Shared.Backmen.Reinforcement.Components;

namespace Content.Server.Backmen.Reinforcement.Components;

[RegisterComponent]
public sealed partial class ReinforcementSpawnerComponent : Component
{
    public Entity<ReinforcementConsoleComponent> Linked;
}
