using Robust.Shared.GameStates;

namespace Content.Shared._Lavaland.Shuttles.Components;

/// <summary>
/// Applied to powered devices placed on the mining shuttle after mapinit.
/// Their APC load is multiplied by <see cref="Multiplier"/>.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class MiningShuttlePowerTaxComponent : Component
{
    [DataField, AutoNetworkedField]
    public float Multiplier = 10f;
}
