using Robust.Shared.GameStates;

namespace Content.Shared.Backmen.Lathe;

/// <summary>
/// Syncs lathe production timing for the medical biofabricator fill animation and UI progress bar.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class BkmBiofabricatorComponent : Component
{
    [AutoNetworkedField]
    public bool IsProducing;

    [AutoNetworkedField]
    public TimeSpan ProductionStart;

    [AutoNetworkedField]
    public TimeSpan ProductionDuration;
}
