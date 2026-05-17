using Robust.Shared.GameStates;

namespace Content.Shared.Backmen.Abilities.Psionics;

[RegisterComponent, NetworkedComponent]
public sealed partial class TelegnosticProjectionComponent : Component
{
    public EntityUid Host;
    /// <summary>
    /// Причина в этом если выдан через статус эффект то Host и его HostComp это разные владельцы
    /// </summary>
    public TelegnosisPowerComponent HostComp;

    public bool IsTrapped = false;
}
